# Simulazione degli eventi

[← Indice del progetto](../project.md)

Test manuali delle Functions della pipeline in locale ([backend-elaborazione.md](../project/backend-elaborazione.md)). Configurazione e avvio in [sviluppo-locale.md](sviluppo-locale.md).

Contenuti: [ValidateUpload](#validateupload-file-caricato-nel-contenitore-degli-originali) · [EnqueueTranscription](#enqueuetranscription-esito-della-normalizzazione-nel-contenitore-dellaudio-normalizzato) · [TranscribeRecord](#transcriberecord-coda-di-trascrizione) · [EnqueueIndexing](#enqueueindexing-esito-della-trascrizione-nel-contenitore-delle-trascrizioni) · [IndexRecord](#indexrecord-coda-di-indicizzazione) · [TagRecords](#tagrecords-coda-dei-tag)

---

Azurite non emette eventi Event Grid, e le sottoscrizioni dello storage dev puntano alla Function App su Azure, non a quella locale. In locale l'evento "blob creato" va quindi inviato a mano all'endpoint dell'host:

```
POST http://localhost:7071/runtime/webhooks/EventGrid?functionName=<NomeFunzione>
aeg-event-type: Notification
```

con un array di eventi `Microsoft.Storage.BlobCreated` nel corpo.

Se la Function App dev su Azure è distribuita, con le sottoscrizioni attive, riceve anch'essa l'evento reale dello stesso upload. Le funzioni sono idempotenti, quindi il doppio passaggio non crea duplicati, ma l'allegato può risultare già creato dalla funzione su Azure.

Gli script in `docs/debug/scripts/` leggono i valori da `docs/debug/scripts/.env`, escluso da git: copiare [docs/debug/scripts/.env.example](scripts/.env.example) in `docs/debug/scripts/.env` e compilare almeno `DATA_STORAGE_ACCOUNT`, `USER_OID` e `RECORD_ID`. Le altre variabili sono facoltative (valori predefiniti nel file di esempio) e servono a provare i diversi casi. Ogni script carica `.env` da sé (`load-env.ps1`): basta modificare il file e rilanciarlo.

## ValidateUpload (file caricato nel contenitore degli originali)

1. Dal frontend in locale (`http://localhost:4200`) fare login e registrare o caricare un audio.
2. Recuperare i due identificativi del percorso del blob `uploads/<oid>/<recordUid>` e scriverli in `docs/debug/scripts/.env` (`USER_OID`, `RECORD_ID`):
   - `recordUid` è nella risposta di `POST /api/uploads/audio` (scheda Network del browser);
   - `oid` è la prima parte del percorso del blob: si trova nel portale o con
     ```powershell
     az storage blob list --auth-mode login --account-name <storage-dati-dev> `
       --container-name uploads --query "[].{name: name, created: properties.creationTime}" -o table
     ```
3. Leggere dimensione e tipo del blob, che l'evento reale riporterebbe:
   ```powershell
   az storage blob show --auth-mode login --account-name <storage-dati-dev> `
     --container-name uploads --name "<oid>/<recordUid>" `
     --query "{size: properties.contentLength, type: properties.contentSettings.contentType}"
   ```
4. Inviare l'evento con [scripts/send-blob-created-event.ps1](scripts/send-blob-created-event.ps1), dopo aver impostato in `.env` dimensione e tipo letti al passo 3 (`UPLOAD_SIZE`, `UPLOAD_CONTENT_TYPE`):
   ```powershell
   .\docs\scripts\send-blob-created-event.ps1
   ```
   Lo script usa `ConvertTo-Json -InputObject`, che mantiene l'array anche con un solo evento: l'endpoint si aspetta un array.

5. Verificare il risultato:
   - nel database, il record in stato `Processing` (creato da `CreateAudioUpload` alla richiesta del SAS, con titolo e data della nota) con il relativo allegato, aggiunto dalla validazione:
     ```sql
     SELECT r.Id, r.RecordUid, r.Title, r.OccurredAt, r.Status, r.ErrorCode, a.OriginalBlobPath, a.OriginalContentType, a.OriginalSizeBytes
     FROM Records r LEFT JOIN Attachments a ON a.RecordId = r.Id
     WHERE r.RecordUid = '<recordUid>';
     ```
   - sulla coda di normalizzazione, il messaggio con l'URL del blob, codificato in base64 (codifica predefinita delle Functions):
     ```powershell
     az storage message peek --auth-mode login --account-name <storage-dati-dev> `
       --queue-name normalization --num-messages 5
     ```

Casi da provare:

| Caso | Come | Risultato atteso |
|---|---|---|
| Duplicato | Reinviare lo stesso evento | Nessun nuovo allegato; nuovo messaggio solo se il record è ancora `Processing` e non normalizzato |
| File vuoto | `UPLOAD_SIZE=0` | Record `Error` con `ErrorCode = file_empty`, nessun messaggio, file conservato |
| File troppo grande | `UPLOAD_SIZE=419430400` (400 MB) | Record `Error` con `ErrorCode = file_too_large`, nessun messaggio, file conservato |
| Percorso non valido | `RECORD_ID=abc` | Evento ignorato (warning nel log), nessuna scrittura |
| Record inesistente | `RECORD_ID` = GUID casuale | Evento ignorato (warning nel log), nessuna scrittura |
| Utente diverso | `USER_OID` = GUID casuale | Evento ignorato (warning nel log), nessuna scrittura |

## EnqueueTranscription (esito della normalizzazione nel contenitore dell'audio normalizzato)

La Function `NormalizeRecord` (coda `normalization`) scrive in `audio` la copia `<oid>/<recordUid>.opus` oppure l'esito `<oid>/<recordUid>.error.json` (vedi [src/functions/README.md](../../src/functions/README.md#normalizzazione-audio-normalizerecord)). `EnqueueTranscription` legge i metadati dal blob, quindi il blob deve esistere davvero: lasciare che `NormalizeRecord` elabori il messaggio con `func start` (serve ffmpeg in locale), oppure caricarlo a mano partendo da un record `Processing` creato con `ValidateUpload`.

1. Caricare il blob con i metadati che scriverebbe `NormalizeRecord`, con [scripts/upload-normalized-blob.ps1](scripts/upload-normalized-blob.ps1). In `.env`, `NORMALIZED_IS_ERROR` sceglie tra la copia normalizzata (file `NORMALIZED_OPUS_FILE`, durata `NORMALIZED_DURATION_SECONDS`) e l'esito di errore (`NORMALIZED_ERROR_CODE`):
   ```powershell
   .\docs\scripts\upload-normalized-blob.ps1
   ```
2. Inviare l'evento con [scripts/send-normalized-event.ps1](scripts/send-normalized-event.ps1): con lo stesso `.env` l'evento riguarda il blob appena caricato (`<recordId>.opus` oppure `<recordId>.error.json`, secondo `NORMALIZED_IS_ERROR`):
   ```powershell
   .\docs\scripts\send-normalized-event.ps1
   ```
3. Verificare il risultato:
   - nel database:
     ```sql
     SELECT r.RecordUid, r.Status, r.ErrorCode, a.NormalizedBlobPath, a.DurationSeconds
     FROM Records r JOIN Attachments a ON a.RecordId = r.Id
     WHERE r.RecordUid = '<recordUid>';
     ```
   - sulla coda di trascrizione, il messaggio JSON `{"blobUrl": "<URL della copia>", "fallbackLanguage": "it"}`, in base64. Con `func start` attivo il messaggio lo consuma subito `TranscribeRecord` (sotto): per vederlo in coda, verificare con le Functions ferme oppure seguire il log di `TranscribeRecord`.
     ```powershell
     az storage message peek --auth-mode login --account-name <storage-dati-dev> `
       --queue-name transcription --num-messages 5
     ```

Casi da provare:

| Caso | Come | Risultato atteso |
|---|---|---|
| Copia normalizzata | `NORMALIZED_IS_ERROR=false` | `NormalizedBlobPath = <oid>/<recordUid>.opus`, `DurationSeconds` dai metadati, record ancora `Processing`, messaggio sulla coda di trascrizione |
| Duplicato della copia | Reinviare lo stesso evento | Allegato invariato, nuovo messaggio finché il record è `Processing` (`TranscribeRecord` deve essere idempotente) |
| Esito di errore | `NORMALIZED_IS_ERROR=true` | Record `Error` con `ErrorCode` dai metadati, nessun messaggio |
| Codice non valido | `NORMALIZED_IS_ERROR=true`, `NORMALIZED_ERROR_CODE=ABC` | Record `Error` con `ErrorCode = processing_failed` |
| Errore dopo la copia | Blob `.error.json` su un record già normalizzato | Esito ignorato (warning), record invariato |
| Record non in elaborazione | Evento su un record `Error` o `Ready` | Evento ignorato, nessuna scrittura |
| Blob inesistente | Evento senza caricare il blob | Evento ignorato (warning), nessuna scrittura |
| Nome non valido | `NORMALIZED_BLOB_NAME=abc.opus` (solo l'evento) | Evento ignorato (warning nel log), nessuna scrittura |

## TranscribeRecord (coda di trascrizione)

`TranscribeRecord` non è attivata da Event Grid ma dalla coda `transcription` dello storage dei dati dev, quindi parte da sola con `func start` quando arriva un messaggio: quelli scritti da `EnqueueTranscription` (sopra), da "riprova" e dalla rigenerazione della trascrizione. Invia la copia normalizzata ad Azure AI Speech (fast transcription) e salva testo e lingua direttamente nel database, senza blob in `transcripts`. Per inviare un messaggio a mano partendo da un record `Processing` con la copia normalizzata già caricata:

```powershell
$url = 'https://<storage-dati-dev>.blob.core.windows.net/audio/<oid>/<recordUid>.opus'
$body = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("{`"blobUrl`":`"$url`",`"fallbackLanguage`":`"it`"}"))
az storage message put --auth-mode login --account-name <storage-dati-dev> `
  --queue-name transcription --content $body
```

Verificare il risultato con la query di `EnqueueIndexing` (sotto) e, in caso di ripiego, la coda del job:

```powershell
az storage message peek --auth-mode login --account-name <storage-dati-dev> `
  --queue-name transcription-fallback --num-messages 5
```

Il timeout della chiamata è di 30 secondi più 10 secondi per minuto di audio (`DurationSeconds` dell'allegato; 30 minuti se manca).

| Caso | Come | Risultato atteso |
|---|---|---|
| Trascrizione | Messaggio valido | `Text` e `OriginalTranscript` uguali, `Language` rilevata (o quella dell'account), messaggio sulla coda di indicizzazione, nessun blob in `transcripts` |
| Duplicato | Reinviare lo stesso messaggio | Nessuna nuova chiamata al servizio: nuovo messaggio di indicizzazione finché il record è `Processing`, poi ignorato |
| Silenzio | Copia normalizzata senza parlato | Record `Error` con `ErrorCode = no_speech`, nessun ripiego |
| Ripiego | `Speech__Endpoint` raggiungibile ma lento, oppure HTTP 429/413 | Messaggio su `transcription-fallback`, record ancora `Processing`; il flusso prosegue dal job e da `EnqueueIndexing` |
| Errore del servizio | Es. `Speech__Endpoint` errato o ruolo mancante (401/403) | La coda riprova fino a 5 volte, poi messaggio su `transcription-fallback` |
| Copia mancante | Messaggio per un record senza blob in `audio` | Record `Error` con `ErrorCode = audio_download_failed` |
| Messaggio non valido | Contenuto non JSON, o URL fuori dal contenitore `audio` | Messaggio scartato (errore nel log), nessuna scrittura |

## EnqueueIndexing (esito della trascrizione nel contenitore delle trascrizioni)

Riguarda solo il ripiego: le trascrizioni di Azure AI Speech vanno direttamente nel database. Il job di trascrizione scrive in `transcripts` la trascrizione `<oid>/<recordUid>.txt` oppure l'esito `<oid>/<recordUid>.error.json` (vedi [src/jobs/transcriber/README.md](../../src/jobs/transcriber/README.md)). Come per `EnqueueTranscription` il blob deve esistere davvero: eseguire il job in locale, oppure caricarlo a mano partendo da un record `Processing`.

1. Caricare il blob con i metadati che scriverebbe il job, con [scripts/upload-transcript-blob.ps1](scripts/upload-transcript-blob.ps1). In `.env`, `TRANSCRIPT_IS_ERROR` sceglie tra la trascrizione (testo fittizio, lingua `TRANSCRIPT_LANGUAGE`) e l'esito di errore (`TRANSCRIPT_ERROR_CODE`):
   ```powershell
   .\docs\scripts\upload-transcript-blob.ps1
   ```
2. Inviare l'evento con [scripts/send-transcript-event.ps1](scripts/send-transcript-event.ps1):
   ```powershell
   .\docs\scripts\send-transcript-event.ps1
   ```
3. Verificare il risultato:
   - nel database:
     ```sql
     SELECT RecordUid, Status, ErrorCode, Language, Text, OriginalTranscript
     FROM Records WHERE RecordUid = '<recordUid>';
     ```
   - sulla coda di indicizzazione, il messaggio con il `recordUid`, in base64:
     ```powershell
     az storage message peek --auth-mode login --account-name <storage-dati-dev> `
       --queue-name indexing --num-messages 5
     ```

Casi da provare:

| Caso | Come | Risultato atteso |
|---|---|---|
| Trascrizione | `TRANSCRIPT_IS_ERROR=false` | `Text` e `OriginalTranscript` uguali al contenuto del blob, `Language` dai metadati, record ancora `Processing`, messaggio sulla coda di indicizzazione |
| Duplicato della trascrizione | Reinviare lo stesso evento | Record invariato, nuovo messaggio finché il record è `Processing` (l'indicizzazione deve essere idempotente) |
| Trascrizione vuota | Caricare a mano un `.txt` vuoto con `az storage blob upload` | Record `Error` con `ErrorCode = no_speech`, nessun messaggio |
| Lingua non valida | `TRANSCRIPT_LANGUAGE=fr` | `Language` = lingua dell'account (warning), messaggio sulla coda |
| Esito di errore | `TRANSCRIPT_IS_ERROR=true` | Record `Error` con `ErrorCode = no_speech` (o quello in `.env`), nessun messaggio |
| Codice non valido | `TRANSCRIPT_IS_ERROR=true`, `TRANSCRIPT_ERROR_CODE=ABC` | Record `Error` con `ErrorCode = processing_failed` |
| Errore dopo la trascrizione | Blob `.error.json` su un record con `OriginalTranscript` già salvata | Esito ignorato (warning), record invariato |
| Record non in elaborazione | Evento su un record `Error` o `Ready` | Evento ignorato, nessuna scrittura |
| Blob inesistente | Evento senza caricare il blob | Evento ignorato (warning), nessuna scrittura |
| Nome non valido | `TRANSCRIPT_BLOB_NAME=abc.txt` (solo l'evento) | Evento ignorato (warning nel log), nessuna scrittura |

## IndexRecord (coda di indicizzazione)

`IndexRecord` non è attivata da Event Grid ma dalla coda `indexing` dello storage dei dati dev, quindi parte da sola con `func start` quando arriva un messaggio: quelli scritti da `TranscribeRecord` ed `EnqueueIndexing` (sopra) o dalla Function HTTP dei testi. Per inviarne uno a mano partendo da un record `Processing` con `Text` valorizzato (il messaggio è il `recordUid` in base64, come lo scrivono le Functions):

```powershell
$recordUid = '<recordUid>'
az storage message put --auth-mode login --account-name <storage-dati-dev> `
  --queue-name indexing --content ([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($recordUid)))
```

Verificare il risultato nel database:

```sql
SELECT RecordUid, Status, ErrorCode FROM Records WHERE RecordUid = '<recordUid>';

SELECT c.ChunkIndex, LEN(c.Content) AS Length, c.EmbeddingModel
FROM RecordChunks c JOIN Records r ON r.Id = c.RecordId
WHERE r.RecordUid = '<recordUid>' ORDER BY c.ChunkIndex;
```

Casi da provare:

| Caso | Come | Risultato atteso |
|---|---|---|
| Indicizzazione | Messaggio per un record `Processing` | Record `Ready`, un chunk ogni 2.000 caratteri circa, `EmbeddingModel = text-embedding-3-small`, messaggio `{"recordUid":…}` sulla coda `tagging` |
| Duplicato | Reinviare lo stesso messaggio | Chunk invariati (record non più `Processing`); il record `Ready` viene riaccodato su `tagging` |
| Reindicizzazione | Riportare il record a `Processing` e reinviare | Chunk sostituiti, non aggiunti |
| Record inesistente | `recordUid` casuale | Messaggio ignorato (warning) |
| Messaggio non valido | Contenuto `abc` in base64 | Messaggio scartato (errore nel log) |
| Testo vuoto | Record `Processing` con `Text` NULL | Record `Error` con `ErrorCode = indexing_failed` |
| Tentativi esauriti | `OpenAI__Endpoint` errato in `local.settings.json` | Dopo 5 tentativi record `Error` con `ErrorCode = indexing_failed`, messaggio in `indexing-poison` |

## TagRecords (coda dei tag)

`TagRecords` è attivata dalla coda `tagging`, scritta da `IndexRecord` quando un record diventa pronto e dalle Function HTTP dei tag quando un tag viene creato, cambia nome o descrizione o viene riattivato. Il messaggio è un JSON con uno solo tra `recordUid` e `tagUid`, in base64 come gli altri:

```powershell
$message = '{"recordUid":"<recordUid>"}'   # oppure '{"tagUid":"<tagUid>"}'
az storage message put --auth-mode login --account-name <storage-dati-dev> `
  --queue-name tagging --content ([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($message)))
```

Verificare il risultato nel database (la soglia è `Tagging__MinSimilarity`, predefinita 0,4; le attese che rendono il tag `Assigning` sono `Records.TagsPendingSince` e `Tags.AssignmentPendingSince`):

```sql
SELECT r.RecordUid, t.TagUid, t.IsActive, rt.Source, rt.Similarity
FROM RecordTags rt
JOIN Records r ON r.Id = rt.RecordId
JOIN Tags t ON t.Id = rt.TagId
WHERE r.RecordUid = '<recordUid>' OR t.TagUid = '<tagUid>';
```

Casi da provare:

| Caso | Come | Risultato atteso |
|---|---|---|
| Record | Messaggio per un record `Ready` | Righe `Auto` per i tag attivi con similarità ≥ soglia |
| Tag | Messaggio per un tag attivo | Righe `Auto` per tutti i record `Ready` simili dell'utente |
| Scelte dell'utente | Riga `Manual` o `Excluded`, poi reinviare | Righe invariate: nessuna riga `Auto` per quella coppia |
| Tag disattivato | `IsActive = 0`, poi messaggio per un record | Le righe `Auto` del tag restano, nessuna nuova |
| Duplicato | Reinviare lo stesso messaggio | Stesse righe, nessun duplicato |
| Record non pronto | Record `Processing` | Messaggio ignorato |
| Messaggio non valido | Contenuto `abc` o `{}` | Messaggio scartato (errore nel log) |
| Fine dell'attesa | Messaggio per un record con `TagsPendingSince` o un tag con `AssignmentPendingSince` | Timestamp tornato NULL insieme alle assegnazioni; `GET /tags` riporta `status = Ready` |
| Attesa più recente | Cambiare il timestamp tra lettura e salvataggio (es. due modifiche ravvicinate del tag) | Il timestamp nuovo resta: il tag rimane `Assigning` fino al messaggio successivo |
| Tentativi esauriti | Eccezione forzata in locale in `TagRecords` (es. `throw` temporaneo prima del salvataggio) | Al quinto tentativo timestamp azzerato, messaggio in `tagging-poison` |
| Timeout | `TagsPendingSince` più vecchio di 15 minuti | Il tag risulta `Ready` anche senza confronto |
