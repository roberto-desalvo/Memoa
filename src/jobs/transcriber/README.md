# Job di trascrizione

Container Apps Job (worker .NET 10 + ffmpeg + Whisper.net) avviato dallo scaler KEDA sulla coda `transcription`. Ogni esecuzione elabora un messaggio e termina.

1. Legge un messaggio dalla coda, invisibile per 65 minuti. Il messaggio è il JSON `{"blobUrl": "...", "fallbackLanguage": "it"}` in base64, scritto da `EnqueueTranscription` (contratto in `src/shared/Pipeline/TranscriptionRequest.cs`).
2. Scarica la copia normalizzata da `audio/<oid>/<RecordUid>.opus` in una cartella temporanea.
3. `ffmpeg` la decodifica in PCM float32 mono a 16 kHz.
4. Whisper rileva la lingua sui primi 30 secondi, scegliendo solo tra italiano e inglese. Se la probabilità è sotto `Whisper__LanguageProbabilityThreshold` (predefinita 0,5) usa `fallbackLanguage`, la lingua dell'account.
5. Trascrive nella lingua scelta, con un prompt iniziale punteggiato in quella lingua (`Whisper__Prompts__it`, `Whisper__Prompts__en`; predefiniti in `JobOptions.cs`) che spinge Whisper a usare la punteggiatura, e scrive **uno** dei due blob nel contenitore `transcripts`, solo se nessuno dei due esiste già (scrittura condizionale):

| Blob | Quando | Metadati |
|---|---|---|
| `<oid>/<RecordUid>.txt` | Trascrizione riuscita (UTF-8) | `language` (lingua usata), `detectedlanguage` e `languageprobability` (esito del rilevamento), `engine` (es. `whisper.net/ggml-large-v3-turbo-q5_0`) |
| `<oid>/<RecordUid>.error.json` | Trascrizione scartata | `errorcode`; corpo `{"errorCode":"..."}` |

6. Elimina il messaggio. L'evento "blob creato" sul contenitore `transcripts` fa proseguire il flusso: la Function deve distinguere i due casi dal suffisso del nome (`Transcript.TryParse`).

Il job non scrive nel database né sulle code e legge solo `audio`. Nei log solo identificativi, lingua, durata dell'elaborazione e numero di caratteri: mai il testo.

## Codici d'errore

Definiti in `src/shared/Pipeline/Transcript.cs`. Ogni codice indica la fase non riuscita; l'eccezione completa è nei log del job (Application Insights) con lo stesso `RecordUid`.

| Codice | Quando | Cosa controllare |
|---|---|---|
| `no_speech` | Subito: trascrizione vuota, nessun parlato riconosciuto | L'audio (silenzio, rumore). Unico codice dovuto al contenuto |
| `invalid_request` | Subito: il messaggio identifica il record ma la lingua di ripiego non è supportata | `Users.PreferredLanguage` e `EnqueueTranscription` |
| `audio_download_failed` | All'ultimo tentativo: lettura della copia normalizzata non riuscita | Ruolo di lettura su `audio` dell'identità del job, rete, spazio su disco della replica |
| `audio_decode_failed` | Subito, senza retry: ffmpeg non decodifica la copia in PCM | ffmpeg nell'immagine (`Job__FfmpegPath`), copia corrotta |
| `model_load_failed` | Subito, senza retry: Whisper non carica il modello | Riga di log "Caricamento del modello": percorso, dimensione del file, memoria disponibile. Poi `Whisper__ModelPath`, checksum, memoria del job |
| `transcription_failed` | All'ultimo tentativo: errore del motore durante rilevamento della lingua o trascrizione | Runtime nativo di Whisper.net, memoria |
| `transcript_upload_failed` | All'ultimo tentativo: scrittura della trascrizione non riuscita | Ruolo di scrittura su `transcripts` dell'identità del job |
| `processing_timeout` | Subito, senza retry: esecuzione interrotta dal SIGTERM allo scadere del `replicaTimeout` | Durata dell'audio rispetto alle vCPU del job |
| `processing_failed` | Messaggio oltre `Job__MaxDequeueCount` tentativi (predefinito 3) senza causa registrata: l'ultima esecuzione è terminata senza poter scrivere l'esito (memoria esaurita, replica arrestata) | Cronologia delle esecuzioni del job in Container Apps |

Gli errori deterministici (`audio_decode_failed`, `model_load_failed`, `processing_timeout`) si ripeterebbero uguali con la stessa immagine e lo stesso audio: il job scrive subito l'esito ed elimina il messaggio. Dopo la correzione l'utente riprova dal record.

Gli altri errori, prima dell'ultimo tentativo, terminano l'esecuzione con codice 1 e un warning con il codice. Il messaggio resta in coda ma **invisibile** per il `Job__VisibilityTimeout` (65 minuti): portale, `az storage message peek` e lo scaler KEDA vedono solo i messaggi visibili, quindi la coda sembra vuota e il record resta "in elaborazione". `ApproximateMessagesCount` della coda invece lo conta. All'ultimo tentativo il job scrive l'esito con il codice della fase ed elimina il messaggio, al posto della coda dei messaggi non elaborabili, su cui il job non può scrivere.

Casi senza esito, con il solo log:
- copia normalizzata non più presente (record o account eliminati): scrivere l'esito lascerebbe un blob senza record;
- messaggio da cui non si ricava il record (JSON errato, campo mancante, URL fuori dallo storage dei dati o dal contenitore `audio`, nome del blob non valido): log di errore con l'id del messaggio.

## Motore di trascrizione

Il job dipende solo da `ITranscriptionEngine` (`Engine/`): riceve il file Opus e la lingua di ripiego e restituisce testo e lingua. L'implementazione attuale è `WhisperEngine` (whisper.cpp su CPU tramite Whisper.net); un'alternativa, ad esempio un'API cloud, si registra al suo posto in `Program.cs`.

Il modello è `ggml-large-v3-turbo-q5_0.bin` (circa 550 MB), incluso nell'immagine dal `Dockerfile`: scaricato da Hugging Face a una revisione fissata e verificato con lo SHA-256. Per cambiarlo vanno aggiornati URL, checksum e `Whisper__ModelPath`.

Velocità e qualità sull'italiano su CPU sono da misurare: con 4 vCPU (dev e prod) una registrazione di 30 minuti potrebbe avvicinarsi al `replicaTimeout` di 60 minuti.

## Configurazione

Variabili d'ambiente impostate da `infra/modules/queue-job.bicep`:

| Variabile | Esempio |
|---|---|
| `DataStorage__BlobServiceUri` | `https://<account>.blob.core.windows.net/` |
| `DataStorage__QueueServiceUri` | `https://<account>.queue.core.windows.net/` |
| `Job__Queue` | `transcription` |
| `Job__InputContainer` | `audio` |
| `Job__OutputContainer` | `transcripts` |
| `AZURE_CLIENT_ID` | Client ID dell'identità `id-memoa-<env>-transcriber` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Facoltativa: senza, i log restano sulla console |

Impostata dal `Dockerfile`: `Whisper__ModelPath`. Facoltative: `Whisper__LanguageProbabilityThreshold`, `Whisper__Threads` (predefinito: le vCPU viste dal processo, che in Container Apps possono superare quelle del job), `Whisper__Prompts__<lingua>` (stringa vuota per trascrivere senza prompt), `Job__MaxDequeueCount`, `Job__VisibilityTimeout` (formato `hh:mm:ss`, deve superare il `replicaTimeout`), `Job__FfmpegPath`.

## Esecuzione in locale

Richiede ffmpeg nel `PATH`, il modello scaricato in locale e un login con `az login`; il proprio utente deve avere sullo storage dati dev gli stessi ruoli dell'identità del job (lettura di `audio`, scrittura su `transcripts`, Queue Data Message Processor su `transcription`). Il runtime nativo per Windows è incluso nel pacchetto `Whisper.net.Runtime`.

```powershell
Invoke-WebRequest https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin -OutFile $env:TEMP\ggml-large-v3-turbo-q5_0.bin
$env:DataStorage__BlobServiceUri = "https://<storage-dati-dev>.blob.core.windows.net/"
$env:DataStorage__QueueServiceUri = "https://<storage-dati-dev>.queue.core.windows.net/"
$env:Job__Queue = "transcription"
$env:Job__InputContainer = "audio"
$env:Job__OutputContainer = "transcripts"
$env:Whisper__ModelPath = "$env:TEMP\ggml-large-v3-turbo-q5_0.bin"
dotnet run --project src/jobs/transcriber
```

Con Docker (il contesto è `src/`, perché l'immagine include anche `src/shared`):

```powershell
docker build -f src/jobs/transcriber/Dockerfile -t memoa-transcriber src
```

## Immagine

La pipeline `.github/workflows/transcriber.yml` pubblica `ghcr.io/<owner>/memoa-transcriber` con i tag `<sha>` e `<branch>`, poi aggiorna il job dell'ambiente (`main` → dev, `prod` → prod) con il tag `<sha>`. Il deploy dell'infrastruttura usa invece `TRANSCRIBER_IMAGE_<ENV>` (il tag del branch): vedi [infra/README.md](../../../infra/README.md#deploy).
