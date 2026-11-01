# Frontend Memoa

PWA Angular. Il login avviene con Entra External ID tramite MSAL (redirect); le chiamate alle Functions portano il token di accesso, aggiunto da `MsalInterceptor`.

La configurazione dell'ambiente è letta a runtime da `public/config.json`, escluso da git; nel deploy su Static Web Apps il file va generato dalla pipeline con i valori dell'ambiente.

Installabile come app: `public/manifest.webmanifest`, icone in `public/icons/` (generate da `favicon.svg`) e service worker di Angular (`ngsw-config.json`), attivo solo nella build di produzione. Il service worker mette in cache solo i file dell'app; `config.json` e le chiamate alle Functions vanno sempre in rete, quindi nessun dato dell'utente resta nella cache. Le nuove versioni vengono proposte con il pulsante "Aggiorna". Con `ng serve` il service worker è disattivato: per provarlo servire `dist/memoa-web/browser` (es. `npx http-server dist/memoa-web/browser`) su `localhost`.

Configurazione e avvio in locale: vedi [docs/debug/sviluppo-locale.md](../../docs/debug/sviluppo-locale.md).
