# Frontend Memoa

PWA Angular. Il login avviene con Entra External ID tramite MSAL (redirect); le chiamate alle Functions portano il token di accesso, aggiunto da `MsalInterceptor`.

La configurazione dell'ambiente è letta a runtime da `public/config.json`, escluso da git; nel deploy su Static Web Apps il file va generato dalla pipeline con i valori dell'ambiente.

Configurazione e avvio in locale: vedi [docs/sviluppo-locale.md](../../docs/sviluppo-locale.md).
