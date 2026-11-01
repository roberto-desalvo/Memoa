# Backend ed elaborazione

[← Indice del progetto](../project.md)

Backend su Azure Functions ed elaborazione asincrona dei record con eventi, code, Functions e un Container Apps Job (ripiego della trascrizione).

Contenuti: [Backend](#backend) · [Normalizzazione](#normalizzazione) · [Componenti](#componenti) · [Flusso audio](#flusso-audio) · [Flusso testo](#flusso-testo) · [Riprova e rigenerazione](#riprova-e-rigenerazione) · [Regole](#regole) · [Event Grid](#event-grid) · [Retry ed errori](#retry-ed-errori) · [Registry delle immagini](#registry-delle-immagini) · [Appendice: alternative scartate](#appendice-alternative-scartate)

---

## Backend

**Decisione**: C# .NET su Azure Functions, piano Flex Consumption.

- Grant gratuito mensile: 250.000 esecuzioni e 100.000 GB-s per sottoscrizione. Lo storage account di sistema delle Functions costa pochi centesimi.
- Senza istanze "always ready" ci sono cold start.
- Niente modelli pesanti (Whisper) nelle Functions: il ripiego della trascrizione gira in un Container Apps Job. ffmpeg invece sì, come binario statico nel pacchetto (vedi [Normalizzazione](#normalizzazione)).

---

## Normalizzazione

**Decisione**: Function `NormalizeRecord`, attivata dalla coda `normalization`, con la build statica di ffmpeg inclusa nel pacchetto di deploy. Sostituisce il Container Apps Job di normalizzazione.

- **Build**: [eugeneware/ffmpeg-static](https://github.com/eugeneware/ffmpeg-static), Linux x64. La release `b6.1.1` contiene ffmpeg 7.0.2 con libopus.
- **Come arriva nel pacchetto**: la pipeline delle Functions scarica il binario, ne verifica lo SHA-256 (release e hash fissati nel workflow) e lo copia con la licenza in `tools/ffmpeg/`. Occupa circa 76 MB.
- **Solo ffmpeg, senza ffprobe**: formato, stream e durata si leggono dal riepilogo che `ffmpeg -i` scrive su stderr. Si risparmiano circa 75 MB nel pacchetto. Quel testo può contenere i metadati del file e non va mai nei log.
- **Permesso di esecuzione**: se lo zip lo perde, al primo uso ffmpeg viene copiato nella cartella temporanea e reso eseguibile.
- **Una normalizzazione alla volta per istanza**: lo spazio temporaneo di Flex Consumption (0,8 GB) basta per un originale da 350 MB, la copia e ffmpeg, non per più file in parallelo.
- **Stesso contratto del job**: copia `.opus` o esito `.error.json` in `audio`. Il flusso prosegue da Event Grid e la Function non accede al database.
- **Effetti**: niente immagine su GHCR, identità, ruoli e pipeline dedicati, né avvio KEDA. Il pacchetto delle Functions cresce di circa 76 MB, con possibile impatto sul cold start.
- **Transizione**: il job resta finché la Function non è verificata in dev. Nel frattempo entrambi leggono la coda, senza effetti doppi grazie all'idempotenza.

---

## Componenti

Tutto sta nello storage account dedicato ai dati utente (blob e code), con un system topic Event Grid.

| Contenitore | Chi scrive | Contenuto | Evento "blob creato" → |
|---|---|---|---|
| `uploads` | client (SAS) | originale `<oid>/<RecordUid>` | `ValidateUpload` |
| `audio` | `NormalizeRecord` | `<oid>/<RecordUid>.opus` (durata nei metadati) oppure `<oid>/<RecordUid>.error.json` | `EnqueueTranscription` |
| `transcripts` | job di trascrizione (solo ripiego) | `<oid>/<RecordUid>.txt` (lingua nei metadati) oppure `<oid>/<RecordUid>.error.json` | `EnqueueIndexing` |

Negli esiti `.error.json` il codice d'errore sta nei metadati del blob.

| Coda | Messaggio | Chi legge |
|---|---|---|
| `normalization` | URL dell'originale | `NormalizeRecord` |
| `transcription` | `TranscriptionRequest`: URL della copia `.opus` e lingua dell'account | `TranscribeRecord` |
| `transcription-fallback` | stesso `TranscriptionRequest` | job di trascrizione (Whisper) |
| `indexing` | RecordUid | `IndexRecord` |
| `upload-expiry` | RecordUid, invisibile fino alla scadenza del SAS + 15 minuti | `ExpireAudioUpload` |

---

## Flusso audio

![Flusso di una nota audio](../schemas/flusso-audio.svg)

1. **`CreateAudioUpload`** (HTTP): riceve titolo e data della nota, crea il record "in elaborazione", sceglie il percorso del blob e restituisce un SAS valido solo per creare quel file. Scrive anche il messaggio ritardato su `upload-expiry`.
2. Il client carica il file direttamente in `uploads`. Se l'upload fallisce chiama **`CancelAudioUpload`**, che elimina il record (409 se il file è già arrivato). Se il client non lo fa, alla scadenza **`ExpireAudioUpload`** elimina il record senza file.
3. **`ValidateUpload`** (Event Grid): controlla la dimensione senza leggere il file (vuoto o oltre 350 MB), collega il file come allegato e scrive l'URL su `normalization`. Se il file non è valido porta il record a "in errore" e non accoda nulla; il file resta, per poterlo analizzare.
4. **`NormalizeRecord`** (coda): `ffmpeg` verifica formato reale e durata e crea la copia Ogg Opus in `audio`. Se il file va scartato scrive invece l'esito `.error.json`.
5. **`EnqueueTranscription`** (Event Grid): per la copia `.opus` salva percorso e durata e scrive su `transcription`; per l'esito porta il record a "in errore".
6. **`TranscribeRecord`** (coda): invia la copia ad Azure AI Speech con timeout di 30 s + 10 s per minuto di audio, salva testo (`Text` e `OriginalTranscript`) e lingua nel database e scrive il RecordUid su `indexing`. Se il servizio non risponde entro il timeout, restituisce 429/413 o fallisce all'ultimo tentativo, inoltra il messaggio su `transcription-fallback`.
7. **Ripiego**: il job di trascrizione (Whisper, solo audio fino a 10 minuti) scrive `.txt` o `.error.json` in `transcripts`. **`EnqueueIndexing`** (Event Grid) salva testo e lingua e scrive su `indexing`, oppure porta il record a "in errore".
8. **`IndexRecord`** (coda): divide il testo in frammenti, ne calcola gli embedding, li sostituisce in una transazione e porta il record a "pronto".

Dettagli di trascrizione ed embedding in [trascrizione-ai.md](trascrizione-ai.md).

---

## Flusso testo

**`CreateTextRecord`** (HTTP) salva testo, titolo e data nel database e scrive il RecordUid su `indexing`. Da lì prosegue come il punto 8 del flusso audio. Il testo resta nel database, mai su Blob Storage né nelle code.

---

## Riprova e rigenerazione

- **`RetryRecord`** (HTTP, record "in errore") riparte dal passo fallito:
  - record con testo: solo indicizzazione, senza perdere le correzioni dell'utente;
  - audio con testo ma senza `OriginalTranscript` (rigenerazione fallita): di nuovo la trascrizione;
  - audio senza testo: di nuovo la normalizzazione, dopo aver eliminato copia normalizzata, trascrizione ed esiti di errore.
- **`RegenerateTranscript`** (HTTP, audio "pronto"): elimina trascrizione ed esito del ripiego, azzera `OriginalTranscript` e accoda di nuovo su `transcription`. Il testo corrente resta finché non arriva quello nuovo. L'indicizzazione si rifà solo se il testo cambia.

Entrambe passano dalla coda di trascrizione, quindi prima Azure AI Speech e poi, se serve, il ripiego.

---

## Regole

- Solo le Functions scrivono nel database e sulle code.
- Il job di trascrizione legge solo la propria coda e scrive solo nel proprio contenitore di destinazione: niente code, niente Azure SQL. È una trasformazione audio → testo indipendente dal motore.
- `NormalizeRecord` scrive solo nel contenitore `audio`: niente database né code, il flusso prosegue dall'evento "blob creato".
- Ogni passaggio scrive in un contenitore diverso da quello che legge, quindi non ci sono cicli.
- L'originale non viene mai cancellato né sovrascritto, salvo eliminazione del record o dell'account. Anche i file scartati dalla validazione restano, per poterli analizzare. "Riprova" e rigenerazione eliminano solo i file derivati (copia normalizzata, trascrizione, esiti).
- Il file di trascrizione su Blob Storage esiste solo quando trascrive il job.
- Messaggi di coda: solo identificativi o URL di blob (senza SAS), mai testi.
- Idempotenza obbligatoria: Event Grid e code consegnano almeno una volta. Gli handler ignorano i messaggi di record non più "in elaborazione" e le Functions HTTP agiscono solo su record nello stato atteso.
- Se l'indicizzazione fallisce il testo è già nel database.
- Nessuna orchestrazione e nessun timer sul database.

---

## Event Grid

- Piano Basic: 100.000 operazioni gratuite al mese, poche migliaia a questi volumi.
- Un system topic sullo storage dei dati, una sottoscrizione per contenitore: filtro su `Microsoft.Storage.BlobCreated` e `subjectBeginsWith: /blobServices/default/containers/<contenitore>/`.
- Endpoint di tipo `AzureFunction` (resourceId della funzione, nessuna chiave), un evento per batch.
- Le sottoscrizioni si possono creare solo con le funzioni già deployate: vanno distribuite dopo il codice ([infra/README.md](../../infra/README.md)).
- Sul piano Flex Consumption il blob trigger a polling non è supportato: si usa solo l'Event Grid trigger.
- Il Container Apps Job non si avvia da Event Grid, ma dalla coda tramite lo scaler KEDA `azure-queue` (`queueLengthStrategy: visibleonly`).
- In locale Azurite non emette eventi: vanno simulati con una POST a `/runtime/webhooks/EventGrid?functionName=<Nome>` e header `aeg-event-type: Notification` (procedura e script in [simulazione-eventi.md](../debug/simulazione-eventi.md)).

---

## Retry ed errori

- **Functions su coda**: massimo 5 consegne (`host.json`), poi la coda dei messaggi non elaborabili. All'ultimo tentativo `TranscribeRecord` passa al ripiego, `NormalizeRecord` scrive l'esito `processing_failed` e `IndexRecord` porta il record a "in errore" (`indexing_failed`).
- **Job di trascrizione**: `replicaRetryLimit: 0`. I retry sono quelli della coda, con massimo 3 consegne e VisibilityTimeout di 65 minuti. Il job non può scrivere sulla coda dei messaggi non elaborabili: all'ultimo tentativo, o per errori deterministici, scrive l'esito `.error.json` (`processing_failed` se la causa non è nota).
- **Event Grid**: solo i retry automatici (30 tentativi in 24 ore), nessun dead-letter. Gli eventi non consegnati vanno persi (gestione da definire).
- Stati del record: "in elaborazione", "pronto", "in errore", con codice d'errore.

---

## Registry delle immagini

**Decisione**: GitHub Container Registry, immagine privata del job di trascrizione (Whisper e modello). Quella della normalizzazione resta solo fino alla rimozione del job.

- Gratuito a questi volumi. Push dalle GitHub Actions con `GITHUB_TOKEN`.
- Container Apps non supporta le identità gestite su registry diversi da ACR: il pull usa un personal access token con scope `read:packages`, salvato in Key Vault.

---

## Appendice: alternative scartate

**Backend**: Backend-as-a-Service (Supabase, Firebase), server su VPS.

**Orchestrazione con Durable Functions**: valutata con backend Azure Storage, SQL e Durable Task Scheduler. Il Durable Task Scheduler (SKU Consumption) costa 0,00429 $ per azione nel listino di West Europe (ottobre 2026): circa 0,04 $ per record con 10 azioni, quindi fuori budget già con poche centinaia di record al mese. Lo SKU Dedicated costa circa 715 $ al mese. La documentazione indica un prezzo "per milione di azioni", in contrasto con il listino: da chiarire se si rivaluta.

**Trigger degli eventi**: blob trigger con source EventGrid. Richiede un endpoint WebHook con la chiave `blobs_extension` nell'URL, URL non modificabile dopo la creazione e una coda sullo storage di sistema delle Functions.

**Normalizzazione in un Container Apps Job** (scelta precedente, worker .NET con ffmpeg nell'immagine): più componenti da gestire (immagine su GHCR, token in Key Vault, identità, pipeline) e avvio più lento tramite KEDA.

**ffmpeg su una condivisione Azure Files montata nella Function**: la condivisione non supporta le identità gestite. Il binario nel pacchetto non ha bisogno di montaggi.

**ffprobe nel pacchetto**: circa 75 MB in più per informazioni che ffmpeg dà già.

**Build di BtbN/FFmpeg-Builds**: binario più grande, e le release datate vengono eliminate dopo un po' (resta solo il tag mobile `latest`), quindi la build non è riproducibile.

**Worker di normalizzazione in bash** (curl + ffmpeg): nessuna telemetria, chiamate REST e parsing XML fragili.

**Esito della normalizzazione**: nessun esito per i file scartati (record bloccati "in elaborazione"); contenitore dedicato agli esiti (una sottoscrizione e una Function in più).

**Notifica di fine upload**: conferma dal client (file orfani e record bloccati se l'app si chiude); evento scritto direttamente sulla coda (nessuna validazione prima del job).

**Notifica di fine trascrizione del job**: chiamata HTTP alla Function; timer sul database (tiene sveglio il database serverless); embedding nel job; scrittura del job sulla coda di indicizzazione (accesso in scrittura alle code).

**Passaggio della trascrizione dal job**: scrittura diretta nel database (due componenti con accesso in scrittura); testo nel messaggio di coda (limite di 64 KB, dati personali nelle code).

**Service Bus**: non necessario a questi volumi.
