# Sviluppo in locale

Istruzioni per configurare, avviare e verificare Memoa sulla propria macchina. Il deploy dell'infrastruttura è descritto in [infra/README.md](../infra/README.md), le scelte di progetto in [project.md](../project.md) e lo schema del database in [schema.md](../schema.md).

In locale non esistono copie delle risorse dati: Functions e strumenti lavorano sulle risorse **dev su Azure** (storage dei dati, code, database). Azurite serve solo come storage di sistema dell'host delle Functions (`AzureWebJobsStorage`).

- [Prerequisiti](#prerequisiti)
- [Configurazione](#configurazione)
- [Avvio](#avvio)
- [Database e migration](#database-e-migration)
- [Simulare gli eventi Event Grid](#simulare-gli-eventi-event-grid)
- [Deploy dell'infrastruttura con Bicep](#deploy-dellinfrastruttura-con-bicep)
- [Buone abitudini](#buone-abitudini)

---

## Prerequisiti

- Azure CLI, Azure Functions Core Tools, .NET SDK, Node.js con npm.
- `dotnet ef` per le migration e go-sqlcmd per applicare gli script (una volta: `winget install sqlcmd`).
- Il proprio Object ID in `DEVELOPER_PRINCIPAL_IDS_DEV` di `infra/.env`, con il deploy dell'infrastruttura dev rieseguito: dà al proprio utente sullo storage dei dati gli stessi ruoli delle Functions (Storage Blob Data Contributor, Storage Blob Delegator, Storage Queue Data Contributor) e `Cognitive Services OpenAI User` sull'account Azure OpenAI dev. Un user delegation SAS firmato in locale non può concedere più dei ruoli dati di chi lo firma, e Owner non ne dà.
  ```powershell
  az ad signed-in-user show --query id -o tsv
  ```
- Accesso al database dev. Per modificare lo schema serve l'account amministratore Entra del server SQL (l'identità delle Functions ha solo lettura e scrittura dei dati).
- Login con Azure CLI sull'account che ha questi ruoli:
  ```powershell
  az login
  ```

## Configurazione

I file con i valori reali sono esclusi da git; nel repository ci sono solo i modelli.

### Variabili d'ambiente (`infra/.env`)

Copiare `infra/.env.example` in `infra/.env` e compilarlo (significato delle variabili in [infra/README.md](../infra/README.md#deploy)). Le variabili `AUTH_AUDIENCE_*`, `AUTH_WEB_CLIENT_ID_*` e `AUTH_API_SCOPE_*` le scrive `infra/identity/deploy.ps1`.

```powershell
# Carica infra/.env nella sessione corrente (il punto iniziale è necessario)
. .\infra\load-env.ps1
```

Se le variabili mancano, la compilazione dei `.bicepparam` si ferma con l'errore `BCP427`, anche nell'editor: VS Code non legge `.env`, quindi per togliere l'errore dall'editor vanno salvate anche come variabili utente (`setx NOME "valore"`) e VS Code va riavviato.

### Functions (`src/functions/local.settings.json`)

Copiare `src/functions/local.settings.example.json` in `src/functions/local.settings.json` e compilare i valori dell'ambiente dev:

| Chiave | Contenuto |
|---|---|
| `Auth__Authority` | `AUTH_AUTHORITY_DEV` di `infra/.env` |
| `Auth__Audience` | `AUTH_AUDIENCE_DEV` |
| `DataStorage__blobServiceUri`, `DataStorage__queueServiceUri` | Endpoint dello storage dei dati dev |
| `Sql__ConnectionString` | Database dev, con `Authentication=Active Directory Default` |
| `OpenAI__Endpoint`, `OpenAI__EmbeddingDeployment` | Endpoint dell'account Azure OpenAI dev e nome del deployment (`text-embedding-3-small`). L'account ha l'autenticazione locale disattivata: l'utente di Azure CLI riceve il ruolo `Cognitive Services OpenAI User` sull'account se il suo object ID è in `DEVELOPER_PRINCIPAL_IDS_DEV` di `infra/.env` |
| `AZURE_TOKEN_CREDENTIALS` | `AzureCliCredential`: fa usare solo l'account di Azure CLI (vedi [Database e migration](#database-e-migration)) |

CORS è già aperto su `http://localhost:4200` nella sezione `Host`.

### Frontend (`src/web/public/config.json`)

La configurazione è letta a runtime da `public/config.json`: copiare `src/web/config.example.json` in `src/web/public/config.json` e compilarlo.

| Campo | Contenuto |
|---|---|
| `apiBaseUrl` | URL base delle Functions (`http://localhost:7071/api` in locale) |
| `auth.authority` | `AUTH_AUTHORITY_<ENV>` di `infra/.env` **senza** il suffisso `/v2.0` |
| `auth.clientId` | `AUTH_WEB_CLIENT_ID_<ENV>` |
| `auth.apiScope` | `AUTH_API_SCOPE_<ENV>` |

`http://localhost:4200` deve essere tra i redirect URI della registrazione del frontend (lo è in dev, vedi `infra/identity/main.dev.bicepparam`), e l'app `Memoa (<env>)` deve essere collegata al flusso utente del tenant esterno.

## Avvio

### Da VS Code

**Terminale → Esegui attività → Memoa: avvia tutto** avvia Azurite, le Functions sulla porta 7071 e il frontend sulla porta 4200 (definizione in `.vscode/tasks.json`). La prima volta eseguire anche l'attività **Web: npm install**.

### A mano

```powershell
# Storage locale per AzureWebJobsStorage (dati in .azurite/)
npx -y azurite@3 --silent --skipApiVersionCheck --location .azurite

# Functions: func start compila il progetto .NET isolated prima di avviare l'host
cd src/functions; func start --port 7071

# Frontend
cd src/web; npm install; npm start
```

Aprire `http://localhost:4200`: si viene mandati al login del tenant esterno e, al ritorno, si arriva alla homepage con la registrazione audio. La prima chiamata autenticata crea l'utente nel database.

## Database e migration

Lo schema è gestito con EF Core Migrations, nel progetto Functions (`src/functions/Data/Migrations`). In dev e prod le migration le applica la pipeline dello schema come script SQL idempotente, **mai** all'avvio delle Functions. I comandi seguenti servono per lavorare in locale sul database dev su Azure.

### Prerequisiti

- `az login` con l'account amministratore Entra del server SQL.
- `Sql__ConnectionString` compilata in `src/functions/local.settings.json`.
- Nella sessione PowerShell, limitare le credenziali alla sola Azure CLI:
  ```powershell
  $env:AZURE_TOKEN_CREDENTIALS = "AzureCliCredential"
  ```
  `Active Directory Default` prova più credenziali in ordine, e Visual Studio e VS Code vengono prima della CLI. Se uno dei due è collegato a un altro account o tenant, il token arriva da lì e il server lo rifiuta con `Login failed for user '<token-identified principal>'. The server is not currently configured to accept this token.` La variabile è la stessa che `local.settings.json` imposta per le Functions, ma `dotnet ef` non legge quel file.

### Creare una migration

```powershell
dotnet ef migrations add <NomeMigration> --project src/functions --output-dir Data/Migrations
```

Non si connette al database. Ogni modifica va fatta in due tempi: prima le aggiunte compatibili, poi le rimozioni o le rinomine in un rilascio successivo. Il codice deve funzionare sia con lo schema vecchio sia con quello nuovo.

Per annullare l'ultima migration non ancora applicata:

```powershell
dotnet ef migrations remove --project src/functions
```

### Applicare le migration al database dev

**Opzione consigliata: script idempotente**, lo stesso formato usato dalla pipeline. Si può rileggere prima di applicarlo e rieseguire senza effetti.

```powershell
# Genera lo script (non si connette al database)
dotnet ef migrations script --idempotent --project src/functions -o "$env:TEMP\memoa-schema.sql"

# Applica lo script con go-sqlcmd
sqlcmd -S "<server>.database.windows.net" -d "sqldb-memoa-dev" `
  --authentication-method ActiveDirectoryDefault `
  -i "$env:TEMP\memoa-schema.sql"
```

**Alternativa rapida:** applicare direttamente con EF Core, usando la connection string di `local.settings.json`. La factory di design-time ha una connection string fittizia, quindi `--connection` è obbligatorio.

```powershell
$cs = (Get-Content src/functions/local.settings.json -Raw | ConvertFrom-Json).Values.Sql__ConnectionString
dotnet ef database update --project src/functions --connection $cs
```

Per tornare a una migration precedente (solo in dev):

```powershell
dotnet ef database update <NomeMigration> --project src/functions --connection $cs
```

### Altri comandi utili

```powershell
# Elenco delle migration e di quelle già applicate
dotnet ef migrations list --project src/functions --connection $cs
```

## Simulare gli eventi Event Grid

Azurite non emette eventi Event Grid, e le sottoscrizioni dello storage dev puntano alla Function App su Azure, non a quella locale. In locale l'evento "blob creato" va quindi inviato a mano all'endpoint dell'host:

```
POST http://localhost:7071/runtime/webhooks/EventGrid?functionName=<NomeFunzione>
aeg-event-type: Notification
```

con un array di eventi `Microsoft.Storage.BlobCreated` nel corpo.

Se la Function App dev su Azure è distribuita, con le sottoscrizioni attive, riceve anch'essa l'evento reale dello stesso upload. Le funzioni sono idempotenti, quindi il doppio passaggio non crea duplicati, ma l'allegato può risultare già creato dalla funzione su Azure.

Gli script in `docs/scripts/` leggono i valori da `docs/scripts/.env`, escluso da git: copiare [docs/scripts/.env.example](scripts/.env.example) in `docs/scripts/.env` e compilare almeno `DATA_STORAGE_ACCOUNT`, `USER_OID` e `RECORD_ID`. Le altre variabili sono facoltative (valori predefiniti nel file di esempio) e servono a provare i diversi casi. Ogni script carica `.env` da sé (`load-env.ps1`): basta modificare il file e rilanciarlo.

### ValidateUpload (file caricato nel contenitore degli originali)

1. Dal frontend in locale (`http://localhost:4200`) fare login e registrare o caricare un audio.
2. Recuperare i due identificativi del percorso del blob `uploads/<oid>/<recordUid>` e scriverli in `docs/scripts/.env` (`USER_OID`, `RECORD_ID`):
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

### EnqueueTranscription (esito della normalizzazione nel contenitore dell'audio normalizzato)

Il job di normalizzazione scrive in `audio` la copia `<oid>/<recordUid>.opus` oppure l'esito `<oid>/<recordUid>.error.json` (vedi [src/jobs/normalizer/README.md](../src/jobs/normalizer/README.md)). La Function legge i metadati dal blob, quindi il blob deve esistere davvero: eseguire il job in locale, oppure caricarlo a mano partendo da un record `Processing` creato con `ValidateUpload`.

1. Caricare il blob con i metadati che scriverebbe il job, con [scripts/upload-normalized-blob.ps1](scripts/upload-normalized-blob.ps1). In `.env`, `NORMALIZED_IS_ERROR` sceglie tra la copia normalizzata (file `NORMALIZED_OPUS_FILE`, durata `NORMALIZED_DURATION_SECONDS`) e l'esito di errore (`NORMALIZED_ERROR_CODE`):
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
   - sulla coda di trascrizione, il messaggio JSON `{"blobUrl": "<URL della copia>", "fallbackLanguage": "it"}`, in base64:
     ```powershell
     az storage message peek --auth-mode login --account-name <storage-dati-dev> `
       --queue-name transcription --num-messages 5
     ```

Casi da provare:

| Caso | Come | Risultato atteso |
|---|---|---|
| Copia normalizzata | `NORMALIZED_IS_ERROR=false` | `NormalizedBlobPath = <oid>/<recordUid>.opus`, `DurationSeconds` dai metadati, record ancora `Processing`, messaggio sulla coda di trascrizione |
| Duplicato della copia | Reinviare lo stesso evento | Allegato invariato, nuovo messaggio finché il record è `Processing` (il job di trascrizione deve essere idempotente) |
| Esito di errore | `NORMALIZED_IS_ERROR=true` | Record `Error` con `ErrorCode` dai metadati, nessun messaggio |
| Codice non valido | `NORMALIZED_IS_ERROR=true`, `NORMALIZED_ERROR_CODE=ABC` | Record `Error` con `ErrorCode = processing_failed` |
| Errore dopo la copia | Blob `.error.json` su un record già normalizzato | Esito ignorato (warning), record invariato |
| Record non in elaborazione | Evento su un record `Error` o `Ready` | Evento ignorato, nessuna scrittura |
| Blob inesistente | Evento senza caricare il blob | Evento ignorato (warning), nessuna scrittura |
| Nome non valido | `NORMALIZED_BLOB_NAME=abc.opus` (solo l'evento) | Evento ignorato (warning nel log), nessuna scrittura |

### EnqueueIndexing (esito della trascrizione nel contenitore delle trascrizioni)

Il job di trascrizione scrive in `transcripts` la trascrizione `<oid>/<recordUid>.txt` oppure l'esito `<oid>/<recordUid>.error.json` (vedi [src/jobs/transcriber/README.md](../src/jobs/transcriber/README.md)). Come per `EnqueueTranscription` il blob deve esistere davvero: eseguire il job in locale, oppure caricarlo a mano partendo da un record `Processing`.

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

### IndexRecord (coda di indicizzazione)

`IndexRecord` non è attivata da Event Grid ma dalla coda `indexing` dello storage dei dati dev, quindi parte da sola con `func start` quando arriva un messaggio: quelli scritti da `EnqueueIndexing` (sopra) o dalla Function HTTP dei testi. Per inviarne uno a mano partendo da un record `Processing` con `Text` valorizzato (il messaggio è il `recordUid` in base64, come lo scrivono le Functions):

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
| Indicizzazione | Messaggio per un record `Processing` | Record `Ready`, un chunk ogni 2.000 caratteri circa, `EmbeddingModel = text-embedding-3-small` |
| Duplicato | Reinviare lo stesso messaggio | Messaggio ignorato (record non più `Processing`), chunk invariati |
| Reindicizzazione | Riportare il record a `Processing` e reinviare | Chunk sostituiti, non aggiunti |
| Record inesistente | `recordUid` casuale | Messaggio ignorato (warning) |
| Messaggio non valido | Contenuto `abc` in base64 | Messaggio scartato (errore nel log) |
| Testo vuoto | Record `Processing` con `Text` NULL | Record `Error` con `ErrorCode = indexing_failed` |
| Tentativi esauriti | `OpenAI__Endpoint` errato in `local.settings.json` | Dopo 5 tentativi record `Error` con `ErrorCode = indexing_failed`, messaggio in `indexing-poison` |

## Deploy dell'infrastruttura con Bicep

Raccolta dei comandi. Struttura dei file, prerequisiti dei tenant esterni e passi manuali sono in [infra/README.md](../infra/README.md). I comandi sono per dev; per prod sostituire `dev` con `prod` nel nome del deploy (`-n memoa-prod`) e nel file dei parametri.

### Preparazione della sessione

```powershell
# Carica infra/.env (i .bicepparam leggono le variabili dall'ambiente)
. .\infra\load-env.ps1

# Verifica che az sia sulla sottoscrizione giusta, non sul tenant esterno
az account show --query "{name: name, id: id}" -o table
az account set -s <sottoscrizione>
```

Se az è rimasto sul tenant esterno, `az deployment sub` fallisce con `SubscriptionNotFound`.

### Controlli prima del deploy

```powershell
# Compila template e parametri (errori di sintassi, variabili mancanti: BCP427)
az bicep build-params -f .\infra\main.dev.bicepparam --stdout > $null

# Validazione da parte di Azure Resource Manager, senza modifiche
az deployment sub validate -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam

# Anteprima delle modifiche
az deployment sub what-if -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam

# Anteprima senza le risorse invariate
az deployment sub what-if -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam `
  --exclude-change-types NoChange Ignore
```

### Deploy

```powershell
# Deploy diretto
az deployment sub create -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam

# Deploy con anteprima what-if e conferma prima di procedere
az deployment sub create -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam --confirm-with-what-if
```

Il deploy è incrementale: le risorse tolte dal template non vengono eliminate, né i ruoli assegnati in precedenza (vedi i passi manuali in [infra/README.md](../infra/README.md#passi-manuali)).

### Registrazioni delle app (tenant esterno)

```powershell
# Da eseguire con az sulla sottoscrizione: passa al tenant esterno, distribuisce e torna indietro
.\infra\identity\deploy.ps1 -Environment dev
```

Salva in `infra/.env` e nella sessione `AUTH_AUDIENCE_DEV`, `AUTH_WEB_CLIENT_ID_DEV` e `AUTH_API_SCOPE_DEV`.

### Primo deploy di un ambiente

Le registrazioni servono a `main.bicep`, l'hostname della Static Web App alle registrazioni, e le sottoscrizioni Event Grid richiedono che le funzioni esistano già:

```powershell
az login
.\infra\identity\deploy.ps1 -Environment dev      # registrazioni, redirect URI senza la Static Web App
az deployment sub create -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam   # deployEventSubscriptions = false
.\infra\identity\deploy.ps1 -Environment dev      # aggiunge l'hostname della Static Web App ai redirect URI
# deploy del codice delle Functions, poi deployEventSubscriptions = true nel .bicepparam e:
az deployment sub create -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam
```

### Budget (una volta per sottoscrizione)

```powershell
az deployment sub create -l westeurope -f infra/budget.bicep `
  -p contactEmails='["tu@example.com"]'
```

### Esito e diagnosi

```powershell
# Stato e output dell'ultimo deploy
az deployment sub show -n memoa-dev --query "{state: properties.provisioningState, outputs: properties.outputs}"

# Operazioni fallite, con il messaggio di errore
az deployment operation sub list -n memoa-dev `
  --query "[?properties.provisioningState=='Failed'].{resource: properties.targetResource.resourceName, error: properties.statusMessage.error.message}" -o table

# Elenco dei deploy a livello di sottoscrizione
az deployment sub list --query "[].{name: name, state: properties.provisioningState, time: properties.timestamp}" -o table
```

Gli errori dei moduli (deploy annidati a livello di resource group) si leggono con `az deployment operation group list -g rg-memoa-dev -n <nome-modulo>`.

## Buone abitudini

- Ogni comando che si connette al database lo risveglia e consuma la quota gratuita di vCore-secondi.
- Chiudere gli strumenti di query (SSMS, Azure Data Studio, estensione mssql) quando si ha finito: le connessioni aperte impediscono la pausa automatica del database serverless.
- Mai valori reali in file versionati: `infra/.env`, `local.settings.json` e `public/config.json` restano fuori da git.
