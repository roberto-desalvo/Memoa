# Frontend Memoa

PWA Angular. Il login avviene con Entra External ID tramite MSAL (redirect); le chiamate alle Functions portano il token di accesso, aggiunto da `MsalInterceptor`.

## Configurazione

La configurazione dell'ambiente è letta a runtime da `public/config.json`, escluso da git: copiare `config.example.json` in `public/config.json` e compilarlo.

| Campo | Contenuto |
|---|---|
| `apiBaseUrl` | URL base delle Functions (`http://localhost:7071/api` in locale) |
| `auth.authority` | `AUTH_AUTHORITY_<ENV>` di `infra/.env` **senza** il suffisso `/v2.0` |
| `auth.clientId` | `AUTH_WEB_CLIENT_ID_<ENV>` |
| `auth.apiScope` | `AUTH_API_SCOPE_<ENV>` |

Nel deploy su Static Web Apps il file va generato dalla pipeline con i valori dell'ambiente.

## Avvio in locale

In VS Code: **Terminale → Esegui attività → Memoa: avvia tutto** avvia Azurite, le Functions e il frontend. La prima volta eseguire anche l'attività **Web: npm install**.

A mano:

```powershell
# Storage locale per AzureWebJobsStorage
npx -y azurite@3 --silent --location .azurite

# Functions (porta 7071, CORS già aperto su http://localhost:4200 in local.settings.json)
cd src/functions; func start

# Frontend
cd src/web; npm install; npm start
```

Aprire `http://localhost:4200`: si viene mandati al login del tenant esterno e, al ritorno, la homepage chiama `GET /api/me` e ne stampa la risposta. `http://localhost:4200` deve essere tra i redirect URI della registrazione del frontend (lo è in dev, vedi `infra/identity/main.dev.bicepparam`), e l'app `Memoa (<env>)` deve essere collegata al flusso utente.
