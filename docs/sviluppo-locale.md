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
- Il proprio Object ID in `DEVELOPER_PRINCIPAL_IDS_DEV` di `infra/.env`, con il deploy dell'infrastruttura dev rieseguito: dà al proprio utente sullo storage dei dati gli stessi ruoli delle Functions (Storage Blob Data Contributor, Storage Blob Delegator, Storage Queue Data Contributor). Un user delegation SAS firmato in locale non può concedere più dei ruoli dati di chi lo firma, e Owner non ne dà.
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

Se la Function App dev su Azure è distribuita, con le sottoscrizioni attive, riceve anch'essa l'evento reale dello stesso upload. Le funzioni sono idempotenti, quindi il doppio passaggio non crea duplicati, ma il record può risultare già creato dalla funzione su Azure.

### ValidateUpload (file caricato nel contenitore degli originali)

1. Dal frontend in locale (`http://localhost:4200`) fare login e registrare o caricare un audio.
2. Recuperare i due identificativi del percorso del blob `uploads/<oid>/<recordUid>`:
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
4. Inviare l'evento:
   ```powershell
   $account = "<storage-dati-dev>"
   $oid = "<oid>"
   $recordUid = "<recordUid>"
   $size = 12345
   $contentType = "audio/webm;codecs=opus"

   $event = @(
     @{
       id          = [guid]::NewGuid().ToString()
       eventType   = "Microsoft.Storage.BlobCreated"
       subject     = "/blobServices/default/containers/uploads/blobs/$oid/$recordUid"
       eventTime   = (Get-Date).ToUniversalTime().ToString("o")
       dataVersion = "1"
       data        = @{
         api           = "PutBlob"
         url           = "https://$account.blob.core.windows.net/uploads/$oid/$recordUid"
         contentType   = $contentType
         contentLength = $size
         blobType      = "BlockBlob"
       }
     }
   )

   Invoke-RestMethod -Method Post `
     -Uri "http://localhost:7071/runtime/webhooks/EventGrid?functionName=ValidateUpload" `
     -Headers @{ "aeg-event-type" = "Notification" } `
     -ContentType "application/json" `
     -Body (ConvertTo-Json -InputObject $event -Depth 5)
   ```
   `-InputObject` mantiene l'array anche con un solo evento: l'endpoint si aspetta un array.

5. Verificare il risultato:
   - nel database, il record in stato `Processing` con il relativo allegato:
     ```sql
     SELECT r.Id, r.RecordUid, r.Status, r.ErrorCode, a.OriginalBlobPath, a.OriginalContentType, a.OriginalSizeBytes
     FROM Records r JOIN Attachments a ON a.RecordId = r.Id
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
| Duplicato | Reinviare lo stesso evento | Nessun nuovo record; nuovo messaggio solo se il record è ancora `Processing` e non normalizzato |
| File vuoto | `$size = 0` | Record `Error` con `ErrorCode = file_empty`, nessun messaggio, file conservato |
| File troppo grande | `$size = 400MB` | Record `Error` con `ErrorCode = file_too_large`, nessun messaggio, file conservato |
| Percorso non valido | `$recordUid = "abc"` | Evento ignorato (warning nel log), nessuna scrittura |
| Utente inesistente | `$oid` = GUID casuale | Eccezione: in Azure Event Grid riprova la consegna |

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
