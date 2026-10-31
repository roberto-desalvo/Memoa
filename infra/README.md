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
| `modules/identities.bicep` | RG | Identità user-assigned di Functions, job di normalizzazione e job di trascrizione |
| `modules/monitoring.bicep` | RG | Log Analytics (limite giornaliero, 30 giorni) + Application Insights |
| `modules/static-web-app.bicep` | RG | Static Web Apps Free |
| `modules/data-storage.bicep` | RG | Storage dati GRS: container `uploads` (originali) / `audio` (normalizzato) / `transcripts`, code `normalization` / `transcription` / `indexing`, system topic Event Grid, soft delete 14 gg, versioning, lifecycle, blocco CanNotDelete; ruoli dei job limitati ai propri contenitori e alla propria coda |
| `modules/event-subscriptions.bicep` | RG | Sottoscrizioni Event Grid "blob creato", una per contenitore, verso le Functions `ValidateUpload`, `EnqueueTranscription`, `EnqueueIndexing` (solo con `deployEventSubscriptions = true`) |
| `modules/key-vault.bicep` | RG | Key Vault (RBAC) con il token GHCR |
| `modules/sql.bicep` | RG del server SQL | Database con offerta gratuita su un server esistente condiviso (il server non è gestito da qui) |
| `modules/openai.bicep` | RG | Azure OpenAI con `text-embedding-3-small` Data Zone Standard |
| `modules/function-app.bicep` | RG | Flex Consumption, storage di sistema dedicato, app settings |
| `modules/container-apps-env.bicep` | RG | Container Apps environment (profilo Consumption) condiviso dai due job |
| `modules/queue-job.bicep` | RG | Container Apps Job attivato dalla propria coda, usato due volte: normalizzazione (`uploads` → `audio`, solo ffmpeg) e trascrizione (`audio` → `transcripts`, Whisper). Nessuna scrittura sulle code, nessun accesso ad Azure SQL |

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
2. deploy del codice delle Functions (con le funzioni Event Grid `ValidateUpload`, `EnqueueTranscription`, `EnqueueIndexing`; se i nomi cambiano, aggiornare `functionNames` in `modules/event-subscriptions.bicep`);
3. impostare `deployEventSubscriptions = true` nel `.bicepparam` e ripetere il deploy dell'infrastruttura.

Nessun dead-letter: gli eventi non consegnati dopo i retry automatici di Event Grid vanno persi. In locale Azurite non emette eventi: vanno simulati.

## Verifiche prima del primo deploy

- Disponibilità in West Europe: piano Flex Consumption, `text-embedding-3-small` in Data Zone Standard, runtime `dotnet-isolated` 10.0 su Flex Consumption.
- Offerta gratuita SQL: la sottoscrizione ammette al massimo 10 database gratuiti, tutti nella regione del primo.

## Passi manuali

1. **Server SQL**: è un server condiviso con altri progetti, gestito fuori da questo template (amministratore Entra ID: account personale; regola firewall `AllowAllWindowsAzureIps` già presente). Nome e resource group si passano con le variabili d'ambiente indicate sopra. Chi esegue il deploy deve poter creare database in quel resource group.
2. **Utente del database** per l'identità gestita delle Functions, da eseguire come amministratore Entra sul database di ogni ambiente. Solo le Functions scrivono nel database: i job di normalizzazione e di trascrizione non hanno un utente SQL.
   ```sql
   CREATE USER [id-memoa-<env>-func] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [id-memoa-<env>-func];
   ALTER ROLE db_datawriter ADD MEMBER [id-memoa-<env>-func];
   ```
   Se l'utente `id-memoa-<env>-transcriber` era già stato creato, rimuoverlo con `DROP USER [id-memoa-<env>-transcriber];`.
3. **Entra External ID**: creare un tenant esterno per ambiente (dev e prod, residenza dati in Europa), i flussi utente e il branding; compilare `AUTH_AUTHORITY_<ENV>` in `infra/.env`. Le registrazioni delle app (frontend e API) si distribuiscono con `identity/deploy.ps1` (vedi [Registrazioni delle app in Entra External ID](#registrazioni-delle-app-in-entra-external-id)); il collegamento dell'app del frontend al flusso utente resta manuale.
4. **GHCR**: quando le immagini dei job sono pubblicate, impostare `normalizerImage`, `transcriberImage` e `ghcrUsername`, e passare `GHCR_TOKEN` (PAT con scope `read:packages`).
5. **GitHub**: secret e variabili del repository (credenziali OIDC per Azure, token di deployment di Static Web Apps, `GHCR_TOKEN`), da configurare a mano.
6. **Ruoli dei job** (solo per ambienti distribuiti prima dell'architettura event-driven): l'identità `id-memoa-<env>-transcriber` aveva i ruoli Storage Blob Data Contributor, Storage Queue Data Contributor e Storage Queue Data Reader sull'intero storage account dati. Il deploy incrementale non li rimuove: eliminarli a mano, perché ora i ruoli dei job sono limitati ai propri contenitori e alla propria coda.
   ```powershell
   $id = az identity show -g rg-memoa-<env> -n id-memoa-<env>-transcriber --query principalId -o tsv
   $st = az storage account list -g rg-memoa-<env> --query "[?ends_with(name, 'data')].id" -o tsv
   az role assignment delete --assignee $id --scope $st
   ```
