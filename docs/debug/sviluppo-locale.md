# Sviluppo in locale

[← Indice del progetto](../project.md)

Configurare e avviare Memoa sulla propria macchina e lavorare sul database. La simulazione degli eventi e i test manuali della pipeline sono in [simulazione-eventi.md](simulazione-eventi.md), il deploy dell'infrastruttura in [infra/README.md](../../infra/README.md).

In locale non ci sono copie delle risorse dati: Functions e strumenti lavorano sulle risorse **dev su Azure** (storage dei dati, code, database, Azure OpenAI, Azure AI Speech). Azurite serve solo come storage di sistema dell'host delle Functions (`AzureWebJobsStorage`).

Contenuti: [Prerequisiti](#prerequisiti) · [Configurazione](#configurazione) · [Avvio](#avvio) · [Database e migration](#database-e-migration) · [Buone abitudini](#buone-abitudini)

---

## Prerequisiti

- Azure CLI, Azure Functions Core Tools, .NET SDK, Node.js con npm.
- `dotnet ef` per le migration e go-sqlcmd per applicare gli script (`winget install sqlcmd`).
- ffmpeg nel `PATH` per la Function `NormalizeRecord` (`winget install Gyan.FFmpeg`), oppure il percorso dell'eseguibile in `Ffmpeg__Path`. In Azure si usa la build statica che la pipeline include nel pacchetto.
- Il proprio Object ID in `DEVELOPER_PRINCIPAL_IDS_DEV` di `infra/.env`, poi un nuovo deploy dell'infrastruttura dev. Dà al proprio utente gli stessi ruoli delle Functions su storage dei dati, Azure OpenAI e Azure AI Speech. Serve perché un SAS firmato in locale non può concedere più dei ruoli di chi lo firma, e Owner non dà accesso ai dati.
  ```powershell
  az ad signed-in-user show --query id -o tsv
  ```
- Per modificare lo schema: l'account amministratore Entra del server SQL.
- `az login` con l'account che ha questi ruoli.

---

## Configurazione

I file con i valori reali sono esclusi da git; nel repository ci sono solo i modelli.

### `infra/.env`

Copiare `infra/.env.example` in `infra/.env` e compilarlo ([variabili](../../infra/README.md#deploy)). `AUTH_AUDIENCE_*`, `AUTH_WEB_CLIENT_ID_*` e `AUTH_API_SCOPE_*` le scrive `infra/identity/deploy.ps1`. Si carica nella sessione con `. .\infra\load-env.ps1`.

### `src/functions/local.settings.json`

Copiare `src/functions/local.settings.example.json` in `src/functions/local.settings.json`. I nomi di contenitori e code sono già compilati; vanno inseriti i valori dell'ambiente dev:

| Chiave | Contenuto |
|---|---|
| `Auth__Authority`, `Auth__Audience` | `AUTH_AUTHORITY_DEV` e `AUTH_AUDIENCE_DEV` di `infra/.env` |
| `DataStorage__blobServiceUri`, `DataStorage__queueServiceUri` | Endpoint dello storage dei dati dev |
| `Sql__ConnectionString` | Database dev, con `Authentication=Active Directory Default` |
| `OpenAI__Endpoint` | Endpoint dell'account Azure OpenAI dev |
| `Speech__Endpoint` | Endpoint di Azure AI Speech dev, con il sottodominio personalizzato |

`AZURE_TOKEN_CREDENTIALS=AzureCliCredential` fa usare solo l'account di Azure CLI ([perché](#database-e-migration)). CORS è già aperto su `http://localhost:4200`.

### `src/web/public/config.json`

Copiare `src/web/config.example.json` in `src/web/public/config.json`:

| Campo | Contenuto |
|---|---|
| `apiBaseUrl` | `http://localhost:7071/api` |
| `auth.authority` | `AUTH_AUTHORITY_DEV` **senza** il suffisso `/v2.0` |
| `auth.clientId` | `AUTH_WEB_CLIENT_ID_DEV` |
| `auth.apiScope` | `AUTH_API_SCOPE_DEV` |
| `maxAudioDurationMinutes` | `30`, come il limite della normalizzazione |

`http://localhost:4200` è già tra i redirect URI della registrazione del frontend in dev (`infra/identity/main.dev.bicepparam`). L'app `Memoa (dev)` deve essere collegata al flusso utente del tenant esterno.

---

## Avvio

**Da VS Code**: **Terminale → Esegui attività → Memoa: avvia tutto** avvia Azurite, le Functions (porta 7071) e il frontend (porta 4200). La prima volta eseguire anche **Web: npm install**.

**A mano**:

```powershell
npx -y azurite@3 --silent --skipApiVersionCheck --location .azurite   # storage di sistema
cd src/functions; func start --port 7071                              # compila e avvia le Functions
cd src/web; npm install; npm start                                    # frontend
```

Aprire `http://localhost:4200`: si viene mandati al login del tenant esterno e poi alla homepage. La prima chiamata autenticata crea l'utente nel database.

Le Functions locali consumano le code dev reali: se anche la Function App dev su Azure è attiva, le due istanze si contendono i messaggi.

---

## Database e migration

Lo schema è gestito con EF Core Migrations in `src/functions/Data/Migrations`. In dev e prod lo applica la pipeline dello schema ([dati.md](../project/dati.md#accesso-ai-dati-e-migration)); i comandi qui servono a lavorare sul database dev.

**Credenziali**: nella sessione PowerShell limitare le credenziali alla sola Azure CLI.

```powershell
$env:AZURE_TOKEN_CREDENTIALS = "AzureCliCredential"
```

`Active Directory Default` prova Visual Studio e VS Code prima della CLI. Se uno dei due è collegato a un altro account, il server rifiuta il token con `Login failed for user '<token-identified principal>'`. `dotnet ef` non legge `local.settings.json`, quindi la variabile va impostata anche nella sessione.

**Creare o annullare una migration** (non si connette al database):

```powershell
dotnet ef migrations add <NomeMigration> --project src/functions --output-dir Data/Migrations
dotnet ef migrations remove --project src/functions    # annulla l'ultima non applicata
```

Ogni modifica va fatta in due tempi: prima le aggiunte compatibili, poi rimozioni e rinomine in un rilascio successivo.

**Applicare al database dev**: consigliato lo script idempotente, lo stesso formato della pipeline.

```powershell
dotnet ef migrations script --idempotent --project src/functions -o "$env:TEMP\memoa-schema.sql"
sqlcmd -S "<server>.database.windows.net" -d "sqldb-memoa-dev" `
  --authentication-method ActiveDirectoryDefault -i "$env:TEMP\memoa-schema.sql"
```

In alternativa, direttamente con EF Core. `--connection` è obbligatorio, perché la factory di design-time ha una connection string fittizia:

```powershell
$cs = (Get-Content src/functions/local.settings.json -Raw | ConvertFrom-Json).Values.Sql__ConnectionString
dotnet ef database update --project src/functions --connection $cs                  # ultima migration
dotnet ef database update <NomeMigration> --project src/functions --connection $cs  # torna indietro (solo dev)
dotnet ef migrations list --project src/functions --connection $cs                  # elenco e stato
```

---

## Buone abitudini

- Ogni comando che si connette al database lo risveglia e consuma la quota gratuita di vCore-secondi.
- Chiudere gli strumenti di query (SSMS, Azure Data Studio, estensione mssql) a fine lavoro: le connessioni aperte impediscono la pausa automatica.
- Mai valori reali in file versionati: `infra/.env`, `local.settings.json`, `public/config.json` e `docs/debug/scripts/.env` restano fuori da git.
