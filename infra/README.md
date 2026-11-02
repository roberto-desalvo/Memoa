# Infrastruttura Memoa

Bicep per tutto ciò che lo supporta; qui sotto i passi manuali per il resto.

## Struttura

| File | Scope | Contenuto |
|---|---|---|
| `main.bicep` | sottoscrizione | Resource group `rg-memoa-<env>` e tutte le risorse di un ambiente |
| `main.dev.bicepparam`, `main.prod.bicepparam` | | Parametri per ambiente |
| `budget.bicep` | sottoscrizione | Budget mensile unico per dev e prod |
| `identity/main.bicep` | tenant esterno | Registrazioni delle app in Entra External ID (estensione Microsoft Graph): API con scope `access_as_user` e token v2, frontend SPA con redirect URI, service principal e consenso dell'amministratore |
| `identity/main.dev.bicepparam`, `identity/main.prod.bicepparam` | | Parametri per ambiente |
| `identity/deploy.ps1` | | Deploy di `identity/main.bicep` nel tenant esterno di un ambiente e salvataggio degli ID in `.env` |
| `modules/identities.bicep` | RG | Identità user-assigned di Functions e job di trascrizione |
| `modules/monitoring.bicep` | RG | Log Analytics (limite giornaliero, 30 giorni) + Application Insights |
| `modules/static-web-app.bicep` | RG | Static Web Apps Free |
| `modules/data-storage.bicep` | RG | Storage dati GRS: container `uploads` (originali) / `audio` (normalizzato) / `transcripts`, code `normalization` / `transcription` / `transcription-fallback` / `indexing` / `tagging` / `upload-expiry`, system topic Event Grid, soft delete 14 gg, versioning, lifecycle, blocco CanNotDelete; ruoli del job limitati ai propri contenitori e alla propria coda |
| `modules/event-subscriptions.bicep` | RG | Sottoscrizioni Event Grid "blob creato", una per contenitore, verso le Functions `ValidateUpload`, `EnqueueTranscription`, `EnqueueIndexing` (solo con `deployEventSubscriptions = true`) |
| `modules/key-vault.bicep` | RG | Key Vault (RBAC) con il token GHCR, leggibile solo dall'identità del job di trascrizione |
| `modules/sql.bicep` | RG del server SQL | Database con offerta gratuita su un server esistente condiviso (il server non è gestito da qui) |
| `modules/openai.bicep` | RG | Azure OpenAI con `text-embedding-3-small` Data Zone Standard |
| `modules/speech.bicep` | RG | Azure AI Speech S0 per la fast transcription chiamata dalla Function `TranscribeRecord`: sottodominio personalizzato (non reversibile), autenticazione locale disattivata, ruolo `Cognitive Services Speech User` all'identità delle Functions e agli sviluppatori |
| `modules/function-app.bicep` | RG | Flex Consumption, storage di sistema dedicato, app settings |
| `modules/container-apps-env.bicep` | RG | Container Apps environment (profilo Consumption) del job di trascrizione |
| `modules/deploy-identity.bicep` | RG | Identità delle pipeline GitHub Actions: federated credential sull'environment GitHub dell'ambiente, Website Contributor sulla Function App, Contributor sulla Static Web App, Container Apps Jobs Contributor sul job di trascrizione |
| `modules/queue-job.bicep` | RG | Container Apps Job attivato dalla propria coda, usato per la trascrizione di ripiego (coda `transcription-fallback`, `audio` → `transcripts`). Motore scelto da `transcriberEngine`: Whisper (solo registrazioni fino a 10 minuti) o Parakeet (fino alla durata massima dell'app). Nessuna scrittura sulle code, nessun accesso ad Azure SQL |

## Deploy

I file `.bicepparam` leggono dall'ambiente i valori da non pubblicare nella repo:

| Variabile | Obbligatoria | Contenuto |
|---|---|---|
| `SQL_SERVER_RESOURCE_GROUP` | sì | Resource group del server SQL condiviso |
| `SQL_SERVER_NAME` | sì | Nome del server SQL condiviso |
| `GHCR_TOKEN` | solo con immagini private | PAT GitHub con scope `read:packages` |
| `AUTH_AUTHORITY_DEV`, `AUTH_AUTHORITY_PROD` | sì | Authority del tenant Entra External ID dell'ambiente (`https://<sottodominio>.ciamlogin.com/<tenant-id>/v2.0`) |
| `AUTH_AUDIENCE_DEV`, `AUTH_AUDIENCE_PROD` | sì | Client ID della registrazione dell'API nel tenant dell'ambiente. Scritta da `identity/deploy.ps1` |
| `AUTH_WEB_CLIENT_ID_DEV`, `AUTH_WEB_CLIENT_ID_PROD` | no (per il frontend) | Client ID della registrazione del frontend. Scritta da `identity/deploy.ps1` |
| `AUTH_API_SCOPE_DEV`, `AUTH_API_SCOPE_PROD` | no (per il frontend) | Scope da richiedere nel frontend (`api://<tenant-id>/memoa-<env>-api/access_as_user`). Scritta da `identity/deploy.ps1` |
| `DEVELOPER_PRINCIPAL_IDS_DEV` | no (per le Functions in locale) | Object ID, separati da virgola, degli utenti che eseguono le Functions in locale contro dev (`az ad signed-in-user show --query id -o tsv`). Ricevono sullo storage dei dati gli stessi ruoli delle Functions: un user delegation SAS firmato in locale non può concedere più dei ruoli dati di chi lo firma, e Owner non ne dà. Ricevono anche `Cognitive Services OpenAI User` sull'account Azure OpenAI, per gli embedding |
| `TRANSCRIBER_IMAGE_DEV`, `TRANSCRIBER_IMAGE_PROD` | no (dopo la prima pubblicazione) | Immagine del job di trascrizione con Whisper da usare nel deploy dell'infrastruttura: `ghcr.io/<owner>/memoa-transcriber:main` per dev, `:prod` per prod. Senza, il deploy rimette l'immagine segnaposto al posto di quella impostata dalla pipeline |
| `TRANSCRIBER_PARAKEET_IMAGE_DEV`, `TRANSCRIBER_PARAKEET_IMAGE_PROD` | no (dopo la prima pubblicazione) | Come sopra, per l'immagine con Parakeet: `ghcr.io/<owner>/memoa-transcriber-parakeet:main` per dev, `:prod` per prod. Il deploy usa quella del motore scelto da `transcriberEngine` |

In locale stanno in `infra/.env`, escluso da git: copiare `infra/.env.example` in `infra/.env`, compilarlo e caricarlo nella sessione con `load-env.ps1`. In GitHub Actions vanno impostate come secret o variabili del repository.

Se le variabili mancano, la compilazione dei `.bicepparam` si ferma con l'errore `BCP427`, anche nell'editor: VS Code non legge `.env`, quindi per togliere l'errore dall'editor vanno salvate anche come variabili utente (`setx NOME "valore"`) e VS Code va riavviato.

```powershell
# Carica infra/.env nella sessione corrente (il punto iniziale è necessario)
. .\infra\load-env.ps1

# Anteprima
az deployment sub what-if -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam

# Deploy di un ambiente
az deployment sub create -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam

# Budget (una volta per sottoscrizione)
az deployment sub create -l westeurope -f infra/budget.bicep \
  -p contactEmails='["tu@example.com"]'
```

Per prod sostituire `dev` con `prod` nel nome del deploy e nel file dei parametri. Se az è rimasto sul tenant esterno, `az deployment sub` fallisce con `SubscriptionNotFound`: controllare con `az account show` e tornare con `az account set -s <sottoscrizione>`.

Il deploy è incrementale: le risorse e i ruoli tolti dal template non vengono eliminati, vanno rimossi a mano.

### Controlli e diagnosi

```powershell
# Compila template e parametri (errori di sintassi, variabili mancanti: BCP427)
az bicep build-params -f .\infra\main.dev.bicepparam --stdout > $null

# Validazione di Azure Resource Manager, senza modifiche
az deployment sub validate -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam

# Anteprima senza le risorse invariate
az deployment sub what-if -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam `
  --exclude-change-types NoChange Ignore

# Deploy con anteprima e conferma
az deployment sub create -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam --confirm-with-what-if

# Stato e output dell'ultimo deploy
az deployment sub show -n memoa-dev --query "{state: properties.provisioningState, outputs: properties.outputs}"

# Operazioni fallite, con il messaggio di errore
az deployment operation sub list -n memoa-dev `
  --query "[?properties.provisioningState=='Failed'].{resource: properties.targetResource.resourceName, error: properties.statusMessage.error.message}" -o table
```

Gli errori dei moduli si leggono con `az deployment operation group list -g rg-memoa-dev -n <nome-modulo>`.

### Registrazioni delle app in Entra External ID

Il tenant esterno non contiene sottoscrizioni, quindi le registrazioni delle app si distribuiscono con un deploy separato a livello di tenant (`az deployment tenant create`), uno per ambiente. `identity/deploy.ps1` fa tutto il giro:

1. carica `infra/.env` e ricava il tenant esterno da `AUTH_AUTHORITY_<ENV>`;
2. legge l'hostname della Static Web App dal deploy `memoa-<env>` della sottoscrizione, se esiste, e lo aggiunge ai redirect URI;
3. passa az sul tenant esterno (con `az login --tenant <id> --allow-no-subscriptions` se serve) e distribuisce `identity/main.bicep` (deploy `memoa-identity-<env>`);
4. riporta az sulla sottoscrizione di partenza;
5. salva gli output in `infra/.env` e nella sessione: `AUTH_AUDIENCE_<ENV>`, `AUTH_WEB_CLIENT_ID_<ENV>`, `AUTH_API_SCOPE_<ENV>`.

Prerequisiti, una volta per tenant esterno:

1. tenant creato a mano e `AUTH_AUTHORITY_<ENV>` compilata in `infra/.env` (vedi [Passi manuali](#passi-manuali));
2. permesso di deploy a livello di tenant. Con l'account amministratore del tenant esterno: nell'interfaccia di amministrazione di Entra, **Identità → Panoramica → Proprietà → Gestione degli accessi per le risorse di Azure = Sì** (elevazione dell'accesso), poi
   ```powershell
   az login --tenant <tenant-id> --allow-no-subscriptions
   az role assignment create --assignee <object-id-del-proprio-utente> --role Owner --scope /
   ```
   L'elevazione dell'accesso si può poi disattivare: l'assegnazione Owner su `/` resta.
3. riportare az sulla sottoscrizione (`az account set -s <sottoscrizione>`): il login sul tenant esterno lo rende l'account predefinito, e con quello `az deployment sub` fallisce con `SubscriptionNotFound`.

Primo deploy di un ambiente (le registrazioni servono a `main.bicep` e l'hostname della Static Web App alle registrazioni):

```powershell
az login                                          # sottoscrizione Azure
.\infra\identity\deploy.ps1 -Environment dev      # registrazioni, redirect URI senza la Static Web App
az deployment sub create -l westeurope -n memoa-dev -p .\infra\main.dev.bicepparam
.\infra\identity\deploy.ps1 -Environment dev      # aggiunge l'hostname della Static Web App ai redirect URI
```

Nei deploy successivi basta rieseguire `deploy.ps1`: il deploy è idempotente (le app sono identificate da `uniqueName`) e i client ID non cambiano. Se si cancella una registrazione, il client ID cambia: rieseguire anche il deploy dell'infrastruttura per aggiornare `Auth__Audience` delle Functions.

Dopo il primo deploy, nel tenant esterno: collegare l'app del frontend (`Memoa (<env>)`) al flusso utente (**Flussi utente → <flusso> → Applicazioni → Aggiungi applicazione**). L'estensione Graph non gestisce i flussi utente.

### Sottoscrizioni Event Grid

Le sottoscrizioni "blob creato" puntano direttamente alle funzioni (endpoint `AzureFunction`), che devono già esistere quando la sottoscrizione viene creata. Per un ambiente nuovo:

1. deploy dell'infrastruttura con `deployEventSubscriptions = false`;
2. deploy del codice delle Functions (con le funzioni Event Grid `ValidateUpload`, `EnqueueTranscription`, `EnqueueIndexing`; se i nomi cambiano, aggiornare `functionNames` in `modules/event-subscriptions.bicep`; per una funzione non ancora implementata lasciare il nome vuoto: la sua sottoscrizione non viene creata). Alla prima distribuzione di `EnqueueIndexing` il codice va distribuito prima di rilanciare il deploy con `deployEventSubscriptions = true`;
3. impostare `deployEventSubscriptions = true` nel `.bicepparam` e ripetere il deploy dell'infrastruttura.

Nessun dead-letter: gli eventi non consegnati dopo i retry automatici di Event Grid vanno persi. In locale Azurite non emette eventi: vanno simulati, vedi [docs/debug/simulazione-eventi.md](../docs/debug/simulazione-eventi.md).

## Verifiche prima del primo deploy

- Disponibilità in West Europe: piano Flex Consumption, `text-embedding-3-small` in Data Zone Standard, runtime `dotnet-isolated` 10.0 su Flex Consumption, fast transcription di Azure AI Speech.
- Offerta gratuita SQL: la sottoscrizione ammette al massimo 10 database gratuiti, tutti nella regione del primo.

## Passi manuali

1. **Server SQL**: è un server condiviso con altri progetti, gestito fuori da questo template (amministratore Entra ID: account personale; regola firewall `AllowAllWindowsAzureIps` già presente). Nome e resource group si passano con le variabili d'ambiente indicate sopra. Chi esegue il deploy deve poter creare database in quel resource group.
2. **Utente del database** per l'identità gestita delle Functions, da eseguire come amministratore Entra sul database di ogni ambiente. Solo le Functions scrivono nel database: il job di trascrizione non ha un utente SQL.
   ```sql
   CREATE USER [id-memoa-<env>-func] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [id-memoa-<env>-func];
   ALTER ROLE db_datawriter ADD MEMBER [id-memoa-<env>-func];
   ```
   Se l'utente `id-memoa-<env>-transcriber` era già stato creato, rimuoverlo con `DROP USER [id-memoa-<env>-transcriber];`.

   Utente per l'identità delle pipeline, che applica lo script dello schema (modifica dello schema e tabella `__EFMigrationsHistory`):
   ```sql
   CREATE USER [id-memoa-<env>-deploy] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_ddladmin ADD MEMBER [id-memoa-<env>-deploy];
   ALTER ROLE db_datareader ADD MEMBER [id-memoa-<env>-deploy];
   ALTER ROLE db_datawriter ADD MEMBER [id-memoa-<env>-deploy];
   ```
3. **Entra External ID**: creare un tenant esterno per ambiente (dev e prod, residenza dati in Europa), i flussi utente e il branding; compilare `AUTH_AUTHORITY_<ENV>` in `infra/.env`. Le registrazioni delle app (frontend e API) si distribuiscono con `identity/deploy.ps1` (vedi [Registrazioni delle app in Entra External ID](#registrazioni-delle-app-in-entra-external-id)); il collegamento dell'app del frontend al flusso utente resta manuale.
4. **GHCR**: prima della prima esecuzione di `transcriber.yml`, impostare `ghcrUsername` nei `.bicepparam`, passare `GHCR_TOKEN` (PAT con scope `read:packages`) e ripetere il deploy dell'infrastruttura, così il job può scaricare l'immagine privata. Dopo la prima pubblicazione impostare `TRANSCRIBER_IMAGE_<ENV>` e `TRANSCRIBER_PARAKEET_IMAGE_<ENV>` (vedi [Deploy](#deploy)). La pipeline di trascrizione richiede anche un deploy dell'infrastruttura dopo questa modifica, che assegna all'identità della pipeline il ruolo sul job di trascrizione.
5. **GitHub**: environment e variabili delle pipeline, da configurare a mano (vedi [Pipeline GitHub Actions](#pipeline-github-actions)).

## Pipeline GitHub Actions

| Workflow | Si avvia su push su `main` o `prod` se cambia | Cosa fa |
|---|---|---|
| `web.yml` | `src/web/**` | Build Angular, poi su ogni ambiente genera `config.json` e carica su Static Web Apps |
| `functions.yml` | `src/functions/**` tranne `Data/Migrations/**`, `src/shared/**` | `dotnet publish`, poi deploy del pacchetto sulla Function App |
| `schema.yml` | `src/functions/Data/Migrations/**` | Genera lo script idempotente delle migration (scaricabile tra gli artifact) e lo applica con `sqlcmd` |
| `transcriber.yml` | `src/jobs/transcriber/**`, `src/shared/**` | Build delle due immagini del job di trascrizione, una per motore con i pesi dei propri modelli, e push su GHCR (`memoa-transcriber` per Whisper e `memoa-transcriber-parakeet`, tag `<sha>` e `<branch>`), poi su ogni ambiente aggiorna il job con il tag `<sha>` dell'immagine del motore già in uso (`container-job-deploy.yml`) |

L'ambiente dipende dal branch: un push su `main` distribuisce su **dev**, un push su `prod` distribuisce su **prod** (il job prod attende l'approvazione dell'environment GitHub `prod`). Assetto temporaneo, in attesa del primo rilascio in produzione. Tutti si possono avviare anche a mano (**Run workflow**), scegliendo il branch dell'ambiente. I file `*-deploy.yml` sono i passi di deploy per un ambiente, richiamati dai workflow principali.

Lo schema e le Functions si distribuiscono separatamente e in ordine non garantito: le modifiche allo schema vanno fatte in due tempi (vedi [docs/debug/sviluppo-locale.md](../docs/debug/sviluppo-locale.md#database-e-migration)).

Autenticazione su Azure con OIDC, senza segreti: ogni ambiente ha l'identità `id-memoa-<env>-deploy` (`modules/deploy-identity.bicep`) con una federated credential sull'environment GitHub omonimo e i ruoli Website Contributor sulla Function App, Contributor sulla Static Web App (per leggerne il token di deployment) e Container Apps Jobs Contributor sul job di trascrizione (per aggiornarne l'immagine con un PATCH del solo template, senza permessi sulle identità gestite). Sul database serve l'utente SQL del passo manuale 2. La connessione al server SQL dai runner GitHub passa dalla regola firewall `AllowAllWindowsAzureIps`.

Configurazione, una volta per ambiente, dopo il deploy dell'infrastruttura:

1. **Settings → Environments**: creare `dev` e `prod`. Su `prod` attivare **Required reviewers**; limitare **Deployment branches** a `main` per `dev` e a `prod` per `prod`.
2. In ciascun environment, le variabili (**Environment variables**, non sono segreti):

   | Variabile | Valore |
   |---|---|
   | `AZURE_CLIENT_ID` | Output `deployIdentityClientId` del deploy `memoa-<env>` |
   | `AZURE_TENANT_ID` | Tenant della sottoscrizione (`az account show --query tenantId -o tsv`), non quello esterno |
   | `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |
   | `SQL_SERVER_NAME` | Come `SQL_SERVER_NAME` in `infra/.env` |
   | `AUTH_AUTHORITY` | Come `AUTH_AUTHORITY_<ENV>` (il suffisso `/v2.0` viene tolto per il frontend) |
   | `AUTH_WEB_CLIENT_ID` | Come `AUTH_WEB_CLIENT_ID_<ENV>` |
   | `AUTH_API_SCOPE` | Come `AUTH_API_SCOPE_<ENV>` |

   ```powershell
   az deployment sub show -n memoa-dev --query properties.outputs.deployIdentityClientId.value -o tsv
   ```
3. Creare l'utente SQL dell'identità di deploy (passo manuale 2).

Function App e Static Web App sono cercate per resource group (`rg-memoa-<env>`), il database per nome (`sqldb-memoa-<env>`).
