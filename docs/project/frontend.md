# Frontend

[← Indice del progetto](../project.md)

Applicazione client: tipo di app, framework, autenticazione, registrazione e upload, aggiornamento dello stato, hosting. Interfaccia e navigazione sono in [ui.md](ui.md).

Contenuti: [App](#app) · [Installazione e service worker](#installazione-e-service-worker) · [Struttura](#struttura) · [Registrazione audio](#registrazione-audio) · [Upload](#upload) · [Aggiornamento dello stato](#aggiornamento-dello-stato) · [Hosting](#hosting) · [Da definire](#da-definire) · [Appendice: alternative scartate](#appendice-alternative-scartate)

---

## App

**Decisione**: PWA in Angular (TypeScript), progettata prima per la larghezza del telefono.

- **Perché una PWA**: evita il costo annuale degli store (Apple in particolare). In futuro si può impacchettare per gli store (es. Trusted Web Activity su Android).
- **Limiti**: la registrazione si interrompe con schermo bloccato o app in background. Su iOS l'installazione è manuale ("Aggiungi alla schermata Home") e le notifiche push funzionano solo con l'app installata.
- **Perché Angular**: pacchetto ufficiale per la PWA, struttura vicina a quella di .NET.

---

## Installazione e service worker

**Decisione**: app installabile con manifest e service worker di Angular (`@angular/service-worker`). La cache contiene solo i file dell'app, nessun dato dell'utente e nessun uso offline dei dati.

- **Manifest**: `public/manifest.webmanifest`, aperta su `/`, finestra `standalone`, orientamento verticale, colore di fondo e della barra di stato uguale al fondo pagina chiaro (`#f4f2ef`). In `index.html` il `theme-color` segue chiaro e scuro del sistema, non la scelta fatta nelle impostazioni dell'app.
- **Icone**: in `public/icons/`, generate una volta da `favicon.svg`: 192 e 512 px con angoli arrotondati, 512 px maskable (fondo pieno, foglio dentro l'area sicura) e 180 px per iOS (`apple-touch-icon`).
- **Cosa va in cache** (`ngsw-config.json`): `index.html`, JS, CSS, favicon e manifest scaricati all'installazione, le icone al primo uso. `config.json`, le chiamate alle Functions e i blob passano sempre dalla rete: nella cache del dispositivo non restano dati personali, quindi logout ed eliminazione dell'account non devono svuotarla.
- **Aggiornamenti**: quando il service worker ha scaricato una nuova versione compare uno snackbar con il pulsante "Aggiorna". La pagina non si ricarica da sola, per non interrompere una registrazione o una nota in scrittura. Fa eccezione la cache non più coerente con il server (`unrecoverable`): lì l'app si ricarica subito.
- **Registrazione**: solo nella build di produzione, quando l'app è stabile (al massimo dopo 30 secondi), per non rallentare l'avvio. Con `ng serve` il service worker è disattivato.
- **Hosting**: `staticwebapp.config.json` serve `ngsw-worker.js`, `ngsw.json`, `index.html` e il manifest con `cache-control: no-cache` e il manifest con il tipo `application/manifest+json`.
- **Costo**: nessuno. I file sono pochi KB in più sul piano Free di Static Web Apps.

---

## Struttura

- Angular 22, componenti standalone, una cartella per funzionalità in `src/web/src/app`: `auth`, `config`, `home`, `notes`, `recording`, `records`, `settings`, `uploads`.
- **Route**: `/` (elenco), `/new/:mode` (nuova nota), `/records/:recordUid`, `/settings`. Tutte protette da `MsalGuard`.
- **Autenticazione**: MSAL (`@azure/msal-angular`) con login a redirect sul tenant Entra External ID dell'ambiente. `MsalInterceptor` aggiunge il token solo alle chiamate verso le Functions, mai verso lo storage.
- **Configurazione**: letta a runtime da `config.json` (URL delle Functions, authority, client ID, scope). La stessa build vale per dev e prod. Il file è generato dalla pipeline per ogni ambiente.

---

## Registrazione audio

**Decisione**: formato nativo del browser, normalizzato poi dalla Function `NormalizeRecord` in Ogg Opus mono 32 kbps VBR. Si conservano sia l'originale sia la copia normalizzata.

- Formato scelto con `MediaRecorder.isTypeSupported()`, in ordine: `audio/webm;codecs=opus`, `audio/ogg;codecs=opus`, `audio/mp4`. Il fallback MP4/AAC serve ai Safari precedenti alla 18.4.
- Il Blob prende il tipo effettivamente scelto dal browser. Nessun bitrate personalizzato.
- Durata massima di 30 minuti: allo scadere la registrazione si ferma.
- Uscendo dalla pagina la registrazione in corso viene scartata.
- La normalizzazione risolve due problemi dei file registrati dal browser: non sono navigabili (seek) e hanno bitrate diversi tra browser.
- Riproduzione su Safari da verificare.

---

## Upload

- **Audio**: primo filtro nel browser per estensione (MP3, M4A/AAC, WAV, FLAC, Ogg/Opus, WebM, AMR/3GP) e dimensione (350 MB). Il formato reale lo verifica `ffmpeg` in `NormalizeRecord`.
  - Il tipo MIME salvato sul blob si ricava dall'estensione, perché il browser non lo indica in modo affidabile per AMR, 3GP e Opus.
  - Upload diretto su Blob Storage con il SAS restituito da `CreateAudioUpload`. Sopra 8 MB il file va a blocchi (Put Block + Put Block List), con 3 tentativi per blocco sugli errori di rete e del server.
  - Il blob esiste, e quindi parte l'evento, solo alla conferma della lista dei blocchi.
  - Se l'upload fallisce il client chiama `CancelAudioUpload`. Il 409 significa che il file è arrivato comunque.
- **Testo**: oggi solo TXT e MD (massimo 10 MB), letti nel browser in UTF-8 e salvati come testo scritto.
- Ogni file diventa una nota separata.

---

## Aggiornamento dello stato

**Decisione**: polling dal client, ogni 10 secondi.

- Attivo solo mentre la pagina (elenco o record) è aperta, l'app è visibile e almeno un record è in elaborazione. Tornando visibile, l'app aggiorna subito.
- La lista resta in memoria tra le pagine e viene sostituita solo se cambia. Un errore durante il polling lascia la lista com'è.
- Nessuna risorsa aggiuntiva. Ritardo pari all'intervallo, funziona solo con l'app aperta.
- Costo: poche decine di migliaia di esecuzioni al mese, dentro il grant di Flex Consumption.
- Web Push, combinabile con il polling, è da valutare in seguito: avvisa anche ad app chiusa, senza costi Azure, ma su iOS solo con app installata. Inviare solo identificativi.

---

## Hosting

**Decisione**: Azure Static Web Apps, piano Free.

- **Piano Free**: 100 GB di banda al mese, 250 MB per ambiente, nessuno SLA.
- **Limiti del piano**: non include il collegamento delle proprie Functions né l'autenticazione personalizzata. Il frontend fa il login con MSAL e chiama direttamente le Functions con il token, con CORS abilitato sulle Functions.
- **Routing**: `staticwebapp.config.json` riscrive le route dell'app su `index.html`.
- **Uso commerciale**: il piano Free non lo permette. In quel caso si valuta la migrazione, per esempio a Cloudflare Pages.

---

## Da definire

- Best practice e architettura interna della PWA, oltre a installazione e service worker.
- Estrazione del testo da DOCX e PDF: [funzionalita.md](funzionalita.md) li prevede, ma oggi non sono accettati.

---

## Appendice: alternative scartate

**Tipo di app**: cross-platform (React Native, Flutter) o nativa, per il costo degli store.

**Framework**: React, Vue, Svelte, Blazor WebAssembly.

**Installazione**: solo manifest, senza service worker (nessuna cache dell'app, prompt di installazione non garantito su tutti i browser, e Web Push richiede comunque un service worker); cache anche dei dati per la lettura offline (titoli e trascrizioni salvati sul dispositivo, da cancellare a logout ed eliminazione dell'account, molto più complessa).

**Formato di registrazione**: solo il formato nativo senza normalizzazione (file non navigabili, bitrate diversi tra browser); codifica uniforme nel browser con un encoder WebAssembly (più codice, dipendenze e consumo di batteria).

**Aggiornamento dello stato**: Azure SignalR Service o Web PubSub. Il piano Free ha 20 connessioni e 20.000 messaggi al giorno, senza SLA; il piano Standard è fuori budget.

**Hosting**: Cloudflare Pages, Netlify, Vercel Hobby, Blob Storage come sito statico.
