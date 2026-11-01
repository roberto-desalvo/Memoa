# CLAUDE.md

Istruzioni per il coding agent che lavora su questo repository.

## Il progetto

Diario intelligente (side project). L'utente registra o carica audio, scrive o carica testo; gli audio vengono trascritti, tutti i record vengono indicizzati con embedding per la ricerca semantica (anche tra lingue: italiano e inglese).

Vincoli non negoziabili:
- **Budget**: circa 10 € al mese di costi Azure complessivi (dev + prod).
- **Utenti**: al massimo 10 nella fase iniziale.
- **GDPR**: tutti i dati in UE, regione **West Europe**.

Il documento delle decisioni (`docs/project.md`, indice dei file in `docs/project/`) è la fonte di verità per funzionalità e scelte tecniche. Se qualcosa in questo file è in conflitto con `project.md`, vale `project.md` e va segnalato.

## Come lavorare

- **Non prendere decisioni architetturali.** Se un task richiede una scelta non documentata (nuova risorsa Azure, nuova libreria importante, cambio di flusso, nuova dipendenza a pagamento), fermati, presenta le opzioni con pro, contro e costi, e chiedi.
- Non aggiungere risorse o servizi Azure a pagamento senza conferma esplicita.
- Non modificare `docs/project.md` né i file in `docs/project/`: le decisioni le registra l'utente.
- Preferisci modifiche piccole e verificabili. Spiega cosa hai cambiato e perché.

## Stack

| Area | Scelta |
|---|---|
| Frontend | Angular (TypeScript), PWA, su Azure Static Web Apps (piano Free) |
| Autenticazione | Microsoft Entra External ID, MSAL nel frontend, token inviato alle Functions (CORS abilitato sulle Functions) |
| Backend | C# .NET su Azure Functions, piano Flex Consumption |
| Database | Azure SQL Database (offerta gratuita, serverless) |
| Accesso ai dati | Entity Framework Core 10 |
| Ricerca vettoriale | Tipo `VECTOR` di Azure SQL, colonna `vector(1536)`, proprietà `SqlVector<float>`, ricerca esatta con `VECTOR_DISTANCE` |
| Embedding | Azure OpenAI `text-embedding-3-small`, deployment Data Zone Standard (EU) |
| File | Azure Blob Storage, storage account dedicato ai dati utente |
| Code | Azure Storage Queue (nello stesso storage account dei dati) |
| Eventi | Event Grid (piano Basic) |
| Normalizzazione audio | Function attivata dalla coda di normalizzazione, con la build statica di ffmpeg (eugeneware/ffmpeg-static, Linux x64, senza ffprobe) scaricata dalla pipeline e inclusa nel pacchetto di deploy; copia Ogg Opus mono 32 kbps |
| Trascrizione | Azure AI Speech (fast transcription, risorsa S0 in West Europe, autenticazione Entra ID) chiamato da una Function; ripiego su Whisper self-hosted (Whisper.net, large-v3-turbo) in un Container Apps Job, worker in C# .NET |
| Registry | GitHub Container Registry (immagini private) |
| IaC | Bicep |
| CI/CD | GitHub Actions |
| Monitoraggio | OpenTelemetry + Application Insights |
| Segreti | Azure Key Vault (solo credenziali non sostituibili, oggi il token GHCR letto dai job) + identità gestite user-assigned, una per componente |

## Architettura dell'elaborazione

### Flusso audio

1. La Function HTTP riceve titolo e data della nota, crea il record "in elaborazione" (senza allegato), sceglie il percorso del blob e restituisce un URL SAS **valido solo per scrivere quel file**. Se l'upload fallisce il client elimina il record; in ogni caso la Function scrive sulla coda `upload-expiry` un messaggio con il solo RecordUid, invisibile fino alla scadenza del SAS più un margine, e la Function attivata da quel messaggio elimina il record se il file non è arrivato.
2. Il client carica il file direttamente nel contenitore degli **audio originali**.
3. Evento Event Grid "blob creato" → **Function di validazione**: controlla il file e lo collega come allegato al record esistente; se valido scrive l'URL del blob sulla **coda di normalizzazione**, altrimenti porta il record a "in errore" senza accodare nulla; il file non viene eliminato, per poterlo analizzare.
4. **Function di normalizzazione** (attivata dalla coda di normalizzazione): `ffmpeg` verifica formato reale e durata (dal riepilogo di `ffmpeg -i`, senza ffprobe) e crea la copia Ogg Opus nel contenitore dell'**audio normalizzato** (`<oid>/<RecordUid>.opus`, durata nei metadati del blob). Se il file va scartato scrive invece nello stesso contenitore l'esito `<oid>/<RecordUid>.error.json`, con il solo codice d'errore.
5. Evento "blob creato" → Function: per la copia `.opus` salva durata e percorso nel database e scrive sulla **coda di trascrizione**; per l'esito `.error.json` porta il record a "in errore".
6. **Function di trascrizione** (attivata dalla coda di trascrizione): invia la copia normalizzata ad **Azure AI Speech** (fast transcription), con timeout di 30 secondi più 10 secondi per minuto di audio, salva testo (in `Text` e `OriginalTranscript`) e lingua **direttamente nel database**, senza file su Blob Storage, e scrive l'identificativo sulla **coda di indicizzazione**.
7. **Ripiego**: se Azure AI Speech non risponde entro il timeout o si superano i limiti, la Function scrive invece il messaggio sulla **coda di ripiego** (`transcription-fallback`), che avvia il **job di trascrizione** con Whisper self-hosted. Il job accetta solo registrazioni fino a 10 minuti (le più lunghe vengono rifiutate) e scrive il file di testo nel contenitore delle **trascrizioni**. Evento "blob creato" → Function: per la trascrizione `<oid>/<RecordUid>.txt` salva testo e lingua nel database e scrive l'identificativo sulla coda di indicizzazione; per l'esito `<oid>/<RecordUid>.error.json` porta il record a "in errore".
8. **Function di indicizzazione**: legge il testo dal database, lo divide in frammenti, calcola l'embedding di ciascun frammento, sostituisce in una transazione i frammenti del record e salva lo stato "pronto".

La rigenerazione della trascrizione e il "riprova" seguono lo stesso percorso: coda di trascrizione, prima Azure AI Speech, poi eventualmente il ripiego.

### Flusso testo

La Function HTTP salva il testo, con titolo e data della nota, nel database e, parallelamente, scrive sulla coda di indicizzazione. Il testo dei record resta nel database (non su Blob Storage).

### Regole dell'architettura (vincolanti)

- **Solo le Functions scrivono nel database e sulla coda di indicizzazione.**
- Il job di trascrizione legge **solo la propria coda** (ripiego della trascrizione), non scrive su nessuna coda né nel database, e scrive **solo su Blob Storage**.
- La Function di normalizzazione scrive solo nel contenitore dell'audio normalizzato (copia o esito di errore), non nel database né sulle code: il flusso prosegue dall'evento "blob creato". Una sola normalizzazione alla volta per istanza (spazio temporaneo di Flex Consumption: 0,8 GB).
- La coda di trascrizione la legge solo la Function; il job di trascrizione ascolta solo la coda di ripiego.
- Il file di testo della trascrizione su Blob Storage esiste **solo quando trascrive il job**; le trascrizioni di Azure AI Speech la Function le salva direttamente nel database.
- Servizio di trascrizione (nella Function) e motore del job stanno dietro **interfacce sostituibili**: il job resta una trasformazione audio → testo indipendente dal motore.
- Ogni passaggio scrive in un contenitore diverso da quello che legge. Ogni sottoscrizione Event Grid filtra sul proprio contenitore (prefisso del soggetto).
- **L'audio originale non viene mai cancellato né sovrascritto** (salvo eliminazione del record o dell'account da parte dell'utente).
- **Idempotenza obbligatoria**: Event Grid e le code consegnano almeno una volta, con possibili duplicati. Ogni handler deve poter ricevere due volte lo stesso messaggio senza effetti indesiderati.
- Sul piano Flex Consumption il blob trigger a polling non è supportato: usare solo il trigger basato su Event Grid.
- Il Container Apps Job si avvia dalla coda (KEDA), non direttamente da Event Grid.
- Nessuna orchestrazione (niente Durable Functions): code e Functions separate.
- Retry tramite i meccanismi delle code (coda dei messaggi non elaborabili). Il job non può scrivere su quella coda: dopo il numero massimo di tentativi scrive l'esito di errore (`processing_failed`); lo stesso esito lo scrive la Function di normalizzazione all'ultimo tentativo. Eventi Event Grid: solo retry automatici, nessun dead-letter per ora.
- Se il calcolo dell'embedding fallisce, la trascrizione è già salvata nel database (e, nel ripiego, resta nel blob): il record non va perso.
- Stati del record: "in elaborazione", "pronto", "in errore"; l'utente può riprovare in caso di errore. Il frontend aggiorna lo stato con **polling** solo finché ci sono record in elaborazione e l'app è visibile.

## Database e migration

- Schema gestito con **EF Core Migrations**.
- Le migration vengono applicate **solo dalla pipeline dedicata allo schema**, come script SQL idempotente (`dotnet ef migrations script --idempotent`). **Mai applicare migration all'avvio delle Functions.**
- Lo schema si distribuisce indipendentemente dal codice, quindi ogni modifica va fatta **in due tempi**: prima le aggiunte compatibili, le rimozioni o rinomine in un rilascio successivo. Il codice deve funzionare sia con lo schema vecchio sia con quello nuovo durante la transizione.
- Accanto a ogni vettore va salvato il **nome del modello di embedding** (cambiarlo richiede di ricalcolare tutti i vettori).
- Non usare indici vettoriali né `VECTOR_SEARCH` (sperimentali, non necessari ora).
- Lo sviluppo locale usa il **database dev su Azure** (nessun database locale). Non lasciare connessioni aperte inutilmente: impediscono la pausa automatica del database serverless e consumano la quota gratuita.
- Niente timer o job che interrogano periodicamente il database.

## Privacy e sicurezza

- **Nei log mai** contenuti dei record, trascrizioni, email, token o altri dati personali: solo identificativi. Verificare cosa raccolgono in automatico le strumentazioni (testo delle query SQL, URL con parametri, indirizzi IP) e disattivare o filtrare ciò che espone dati.
- **Nei messaggi di coda solo identificativi o URL di blob**, mai testi o trascrizioni.
- Mai dati personali nei parametri URL o nelle query string.
- Comunicazione tra servizi con **identità gestite user-assigned**, una per componente (Functions, job di trascrizione, deploy), nessuna system-assigned; in Key Vault solo le credenziali non sostituibili. **Mai segreti nel repository**, nei file di configurazione versionati o nei log.
- Principio del minimo privilegio: ogni identità ha solo i permessi che le servono (es. i job non hanno accesso in scrittura alle code né ad Azure SQL).
- Ogni Function HTTP verifica il token e opera **solo sui dati dell'utente autenticato**.
- Le funzionalità di esportazione ed eliminazione account devono coprire tutti i dati dell'utente: database, tutti i contenitori blob e i messaggi in coda.

## Vincoli funzionali

- Lingue: italiano e inglese. Lingua dell'account scelta alla registrazione e modificabile; la lingua di ogni registrazione è rilevata in automatico, con ripiego sulla lingua dell'account se il rilevamento è incerto.
- **Audio**: MP3, M4A/AAC, WAV, FLAC, Ogg/Opus, WebM, AMR/3GP, verificati sul formato reale con `ffmpeg` (non sull'estensione); nessun file video; massimo 350 MB per file; durata massima 30 minuti.
- **Testo**: tutti i formati di testo, inclusi DOCX e PDF; massimo 10 MB per file e 50.000 caratteri di testo estratto.
- **Registrazione nel browser**: scegliere il formato con `MediaRecorder.isTypeSupported()` (preferenza Opus/WebM, fallback MP4/AAC), creare il Blob con il tipo effettivamente scelto, nessun bitrate personalizzato.

## Infrastruttura

- Tutte le risorse in **West Europe**.
- Due ambienti, **dev** e **prod**, nella stessa sottoscrizione.
- Bicep per tutto ciò che supporta, incluse le registrazioni delle app Entra External ID tramite l'estensione Microsoft Graph, con un deploy separato a livello di tenant (`targetScope = 'tenant'`) per ciascun tenant esterno.
- I tenant Entra External ID sono collegati alla sottoscrizione tramite **gruppi di risorse dedicati non gestiti da Bicep**: non includerli mai nei deployment stacks.
- Authority e audience di Entra External ID sono **per ambiente** (un tenant per ambiente).
- Pipeline GitHub Actions separate: frontend, Functions (che include ffmpeg nel pacchetto), schema del database, immagine di trascrizione.
- Il Container Apps Job di normalizzazione (`src/jobs/normalizer`, `normalizer.yml`, identità e ruoli in Bicep) è in dismissione: resta finché la Function non è verificata in dev.
- Storage account dei dati: soft delete e versioning (14 giorni), ridondanza GRS, blocco CanNotDelete.
- In locale Azurite non emette eventi Event Grid: vanno simulati.

## Ancora da definire (non decidere al posto dell'utente)

- Struttura del repository, versioni di .NET e Angular, framework di test, convenzioni di codice.
- Best practice e architettura interna della PWA.
- Conservazione dei file di trascrizione dopo il salvataggio nel database.
- Gestione degli eventi Event Grid non consegnati.
- Funzionalità avanzate (immagini con OCR/HTR, video, Q&A, riassunti, categorie, tag) e relativi modelli.

Se un task tocca uno di questi punti, chiedi prima di procedere.

## Comandi

<!-- Da completare quando il repository avrà una struttura: build, test, avvio locale, lint. -->