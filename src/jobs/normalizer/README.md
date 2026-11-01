# Job di normalizzazione

> **In dismissione.** La normalizzazione è passata alla Function `NormalizeRecord` ([src/functions/README.md](../../functions/README.md#normalizzazione-audio-normalizerecord)), che legge la stessa coda e scrive gli stessi blob. Job, identità, ruoli e `normalizer.yml` vengono rimossi quando la Function è verificata in dev.

Container Apps Job (worker .NET 10 + ffmpeg) avviato dallo scaler KEDA sulla coda `normalization`. Ogni esecuzione elabora un messaggio e termina.

1. Legge un messaggio dalla coda (l'URL del blob originale, in base64, scritto da `ValidateUpload`), invisibile per 35 minuti.
2. Scarica l'originale da `uploads/<oid>/<RecordUid>` in una cartella temporanea.
3. `ffprobe` sul formato reale; se valido, `ffmpeg` produce la copia Ogg Opus, mono, 32 kbps VBR, profilo `voip`, senza metadati del file caricato.
4. Scrive **uno** dei due blob nel contenitore `audio`, solo se nessuno dei due esiste già (scrittura condizionale):

| Blob | Quando | Metadati |
|---|---|---|
| `<oid>/<RecordUid>.opus` | File valido | `durationseconds` (intero, arrotondato per eccesso), `sourceformat` (`format_name` di ffprobe) |
| `<oid>/<RecordUid>.error.json` | File scartato | `errorcode`; corpo `{"errorCode":"..."}` |

5. Elimina il messaggio. L'evento "blob creato" sul contenitore `audio` fa proseguire il flusso: la Function deve distinguere i due casi dal suffisso del nome.

Il job non scrive nel database né sulle code e legge solo `uploads`.

## Codici d'errore

| Codice | Causa |
|---|---|
| `format_not_supported` | ffprobe non riconosce il file, o il formato non è tra MP3, M4A/AAC, WAV, FLAC, Ogg, WebM, AMR/3GP |
| `video_not_supported` | Il file contiene una traccia video (le copertine di MP3/M4A sono ammesse) |
| `no_audio` | Nessuna traccia audio |
| `audio_empty` | La copia normalizzata ha durata nulla |
| `audio_too_long` | Oltre 30 minuti. Le registrazioni WebM del browser non hanno la durata nell'header: la conversione si ferma a 30 minuti e 1 secondo e il controllo si fa sulla copia |
| `processing_failed` | Il messaggio ha superato `Job__MaxDequeueCount` tentativi (predefinito 3). Sostituisce la coda dei messaggi non elaborabili, su cui il job non può scrivere |

Errori temporanei (rete, ffmpeg che fallisce) terminano l'esecuzione con codice 1: il messaggio torna visibile dopo il timeout e viene riprovato. Se l'originale non esiste più (record o account eliminati) il messaggio viene eliminato senza scrivere nulla.

## Configurazione

Variabili d'ambiente impostate da `infra/modules/queue-job.bicep`:

| Variabile | Esempio |
|---|---|
| `DataStorage__BlobServiceUri` | `https://<account>.blob.core.windows.net/` |
| `DataStorage__QueueServiceUri` | `https://<account>.queue.core.windows.net/` |
| `Job__Queue` | `normalization` |
| `Job__InputContainer` | `uploads` |
| `Job__OutputContainer` | `audio` |
| `AZURE_CLIENT_ID` | Client ID dell'identità `id-memoa-<env>-normalizer` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Facoltativa: senza, i log restano sulla console |

Facoltative: `Job__MaxDequeueCount`, `Job__VisibilityTimeout` (formato `hh:mm:ss`), `Job__FfmpegPath`, `Job__FfprobePath`.

## Esecuzione in locale

Richiede ffmpeg nel `PATH` e un login con `az login`; il proprio utente deve avere sullo storage dati dev gli stessi ruoli dell'identità del job (lettura di `uploads`, scrittura su `audio`, Queue Data Message Processor su `normalization`).

```powershell
$env:DataStorage__BlobServiceUri = "https://<storage-dati-dev>.blob.core.windows.net/"
$env:DataStorage__QueueServiceUri = "https://<storage-dati-dev>.queue.core.windows.net/"
$env:Job__Queue = "normalization"
$env:Job__InputContainer = "uploads"
$env:Job__OutputContainer = "audio"
dotnet run --project src/jobs/normalizer
```

Con Docker (il contesto è `src/`, perché l'immagine include anche `src/shared`):

```powershell
docker build -f src/jobs/normalizer/Dockerfile -t memoa-normalizer src
```

## Immagine

La pipeline `.github/workflows/normalizer.yml` pubblica `ghcr.io/<owner>/memoa-normalizer` con i tag `<sha>` e `<branch>`, poi aggiorna il job dell'ambiente (`main` → dev, `prod` → prod) con il tag `<sha>`. Il deploy dell'infrastruttura usa invece `NORMALIZER_IMAGE_<ENV>` (il tag del branch): vedi [infra/README.md](../../../infra/README.md#deploy).
