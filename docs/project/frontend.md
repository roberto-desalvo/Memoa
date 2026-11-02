# Frontend

[← Indice del progetto](../project.md)

Applicazione client: tipo di app, framework, autenticazione, registrazione e upload, aggiornamento dello stato, hosting. Interfaccia e navigazione sono in [ui.md](ui.md).

Contenuti: [App](#app) · [Installazione e service worker](#installazione-e-service-worker) · [App Android](#app-android) · [Struttura](#struttura) · [Registrazione audio](#registrazione-audio) · [Upload](#upload) · [Aggiornamento dello stato](#aggiornamento-dello-stato) · [Hosting](#hosting) · [Da definire](#da-definire) · [Appendice: alternative scartate](#appendice-alternative-scartate)

---

## App

**Decisione**: PWA in Angular (TypeScript), progettata prima per la larghezza del telefono.

- **Perché una PWA**: evita il costo annuale degli store (Apple in particolare). Su Android è distribuita anche come APK (Trusted Web Activity, vedi [App Android](#app-android)).
- **Limiti**: la registrazione si interrompe con schermo bloccato o app in background. Su iOS l'installazione è manuale ("Aggiungi alla schermata Home") e le notifiche push funzionano solo con l'app installata.
- **Perché Angular**: pacchetto ufficiale per la PWA, struttura vicina a quella di .NET.

---

## Installazione e service worker

**Decisione**: app installabile con manifest e service worker di Angular (`@angular/service-worker`). La cache contiene solo i file dell'app, nessun dato dell'utente e nessun uso offline dei dati.

- **Manifest**: `public/manifest.webmanifest`, aperta su `/`, finestra `standalone`, orientamento verticale, colore della barra di stato uguale al fondo pagina chiaro (`#f4f2ef`), colore di fondo (schermata di avvio) uguale al fondo dell'icona (`#6e4bf2`). In `index.html` il `theme-color` segue chiaro e scuro del sistema, non la scelta fatta nelle impostazioni dell'app.
- **Icone**: in `public/icons/`, generate una volta da `favicon.svg`: 192 e 512 px con angoli arrotondati, 512 px maskable (fondo pieno, foglio dentro l'area sicura) e 180 px per iOS (`apple-touch-icon`).
- **Cosa va in cache** (`ngsw-config.json`): `index.html`, JS, CSS, favicon e manifest scaricati all'installazione, le icone al primo uso. `config.json`, le chiamate alle Functions e i blob passano sempre dalla rete: nella cache del dispositivo non restano dati personali, quindi logout ed eliminazione dell'account non devono svuotarla.
- **Aggiornamenti**: quando il service worker ha scaricato una nuova versione compare uno snackbar con il pulsante "Aggiorna". La pagina non si ricarica da sola, per non interrompere una registrazione o una nota in scrittura. Fa eccezione la cache non più coerente con il server (`unrecoverable`): lì l'app si ricarica subito.
- **Registrazione**: solo nella build di produzione, quando l'app è stabile (al massimo dopo 30 secondi), per non rallentare l'avvio. Con `ng serve` il service worker è disattivato.
- **Hosting**: `staticwebapp.config.json` serve `ngsw-worker.js`, `ngsw.json`, `index.html` e il manifest con `cache-control: no-cache` e il manifest con il tipo `application/manifest+json`.
- **Costo**: nessuno. I file sono pochi KB in più sul piano Free di Static Web Apps.

---

## App Android

**Decisione**: APK Trusted Web Activity distribuito a mano, accanto alla PWA. Nessuno store.

- **Cosa contiene**: solo il guscio Android (`src/android`, progetto Gradle con `androidbrowserhelper`, nessun codice proprio) che apre la Static Web App a schermo intero in Chrome. Frontend, service worker e backend sono gli stessi della PWA: gli aggiornamenti del frontend arrivano all'app senza un nuovo APK.
- **Ambienti**: un APK per ambiente, `app.memoa` (prod, "Memoa") e `app.memoa.dev` (dev, "Memoa Dev"), installabili insieme. L'host della Static Web App è scritto nell'APK: se cambia (es. dominio personalizzato) va ridistribuito.
- **Firma**: una chiave per i due ambienti, nei segreti del repository GitHub (con una copia fuori da GitHub). L'impronta è pubblicata in `/.well-known/assetlinks.json`, generato da `web-deploy.yml` per ogni ambiente: senza, l'app mostra la barra dell'URL.
- **Distribuzione**: dev come artefatto della run di `android.yml`, prod come GitHub Release (pubblica, il repository è pubblico). Installazione manuale con le app da origini sconosciute consentite, nessun aggiornamento automatico dell'APK.
- **Aspetto**: icona adattiva dall'icona maskable, schermata di avvio con l'icona su `#6e4bf2`, barre di sistema `#f4f2ef` (chiaro) e `#141316` (scuro), orientamento verticale. Android 8 o successivo.
- **Login**: il redirect di MSAL esce dall'origine verso Entra External ID, quindi durante il login compare la barra dell'URL; nessuna modifica alle registrazioni delle app.
- **Costo**: nessuno (niente Play Store).

---

## Struttura

- Angular 22, componenti standalone, una cartella per funzionalità in `src/web/src/app`: `account`, `auth`, `config`, `guide`, `home`, `notes`, `recording`, `records`, `settings`, `tags`, `uploads`.
- **Route**: `/` (elenco), `/new/:mode` (nuova nota), `/records/:recordUid`, `/tags`, `/tags/new`, `/tags/:tagUid`, `/settings`, `/about`. Tutte protette da `MsalGuard`.
- **Autenticazione**: MSAL (`@azure/msal-angular`) con login a redirect sul tenant Entra External ID dell'ambiente. `MsalInterceptor` aggiunge il token solo alle chiamate verso le Functions, mai verso lo storage.
- **Registrazione dell'utente applicativo**: la homepage chiama `GET /me` (`AccountState`, una sola chiamata anche con richieste concorrenti, poi la risposta resta in memoria per la sessione) e solo dopo carica la lista. Un interceptor riconosce `403 user_not_registered` su qualunque chiamata, azzera lo stato e torna alla homepage, che chiama di nuovo `/me`.
- **Guida**: componente proprio su `@angular/cdk` Overlay (`guide/`), nessuna libreria esterna. Gli elementi evidenziati hanno l'attributo `data-guide`.
- **Configurazione**: letta a runtime da `config.json` (URL delle Functions, authority, client ID, scope, durata massima degli audio). La stessa build vale per dev e prod. Il file è generato dalla pipeline per ogni ambiente.
- **Durata massima degli audio** (`maxAudioDurationMinutes`, oggi 30 minuti): si imposta in un solo punto, il parametro Bicep `maxAudioDurationMinutes`, che diventa l'impostazione `Audio__MaxDurationMinutes` delle Functions (usata dalla normalizzazione). La pipeline del frontend la legge dalla Function App e la scrive in `config.json`. In locale va riportata a mano in `local.settings.json` e in `public/config.json`.

---

## Registrazione audio

**Decisione**: formato nativo del browser, normalizzato poi dalla Function `NormalizeRecord` in Ogg Opus mono 32 kbps VBR. Si conservano sia l'originale sia la copia normalizzata.

- Formato scelto con `MediaRecorder.isTypeSupported()`, in ordine: `audio/webm;codecs=opus`, `audio/ogg;codecs=opus`, `audio/mp4`. Il fallback MP4/AAC serve ai Safari precedenti alla 18.4.
- Il Blob prende il tipo effettivamente scelto dal browser. Nessun bitrate personalizzato.
- Durata massima da `maxAudioDurationMinutes` (30 minuti): allo scadere la registrazione si ferma.
- Uscendo dalla pagina la registrazione in corso viene scartata.
- La normalizzazione risolve due problemi dei file registrati dal browser: non sono navigabili (seek) e hanno bitrate diversi tra browser.
- Riproduzione su Safari da verificare.

---

## Upload

- **Audio**: primo filtro nel browser per estensione (MP3, M4A/AAC, WAV, FLAC, Ogg/Opus, WebM, AMR/3GP), dimensione (350 MB) e durata (`maxAudioDurationMinutes`). Il formato reale e la durata li verifica `ffmpeg` in `NormalizeRecord`.
  - La durata si legge dai metadati con un elemento `<audio>`, senza caricare il file. Se il browser non riconosce il formato (es. AMR/3GP) o la durata non è nell'header (WebM registrati da un browser) il controllo si salta e decide la normalizzazione.
  - Margine del 5%: per gli MP3 a bitrate variabile senza header Xing/VBRI la durata è stimata. Nel margine decide la normalizzazione.
  - Il tipo MIME salvato sul blob si ricava dall'estensione, perché il browser non lo indica in modo affidabile per AMR, 3GP e Opus.
  - Upload diretto su Blob Storage con il SAS restituito da `CreateAudioUpload`. Sopra 8 MB il file va a blocchi (Put Block + Put Block List), con 3 tentativi per blocco sugli errori di rete e del server.
  - Il blob esiste, e quindi parte l'evento, solo alla conferma della lista dei blocchi.
  - Se l'upload fallisce il client chiama `CancelAudioUpload`. Il 409 significa che il file è arrivato comunque.
- **File di testo**: PDF, MD e TXT, caricati su Blob Storage come l'audio, con lo stesso servizio (`AudioUploadService`, tipo `document`, SAS da `CreateDocumentUpload`); il testo lo estrae il backend. Nel browser si controllano estensione, dimensione (16 KB per TXT e MD, 1 MB per i PDF) e, per TXT e MD, testo vuoto o oltre 8.000 caratteri. Il tipo MIME salvato sul blob si ricava dall'estensione.
- Ogni file diventa una nota separata.
- **Coda degli upload** (`UploadQueue`): i file scelti passano prima da un'anteprima con titolo e data modificabili, senza chiamare le Functions; i controlli nel browser (formato, dimensione, durata, lettura del testo) avvengono già lì. La coda è un servizio dell'app, in memoria: gli upload continuano cambiando pagina e si perdono chiudendo l'app; nulla viene salvato oltre al flusso normale.
  - Annullare un upload in corso (audio o file di testo) interrompe le richieste allo storage e chiama `CancelAudioUpload` (il 409 lascia la nota). Se il SAS non era ancora arrivato il record lo elimina la coda `upload-expiry`. Lo stesso vale lasciando la pagina di registrazione durante il salvataggio.

---

## Aggiornamento dello stato

**Decisione**: polling dal client, con gli stessi intervalli per note e tag, definiti in un solo punto (`src/web/src/app/config/polling.ts`): 10 secondi per le liste (homepage, elenco dei tag), 1 secondo per la pagina di una nota o di un tag.

- Attivo solo mentre la pagina è aperta, l'app è visibile e qualcosa è in corso: una nota in elaborazione (homepage, pagina del record) o un tag in assegnazione (elenco dei tag, pagina del tag). Tornando visibile, l'app aggiorna subito. Il controllo successivo parte solo dopo la risposta.
- La lista resta in memoria tra le pagine e viene sostituita solo se cambia. Un errore durante il polling lascia la lista com'è.
- Nessuna risorsa aggiuntiva. Ritardo pari all'intervallo, funziona solo con l'app aperta.
- Costo: poche decine di migliaia di esecuzioni al mese, dentro il grant di Flex Consumption.
- I tag automatici arrivano dopo lo stato "pronto": la pagina del record si ricarica una sola volta, 5 secondi dopo il passaggio a "pronto".
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

---

## Appendice: alternative scartate

**Tipo di app**: cross-platform (React Native, Flutter) o nativa, per il costo degli store.

**Framework**: React, Vue, Svelte, Blazor WebAssembly.

**Installazione**: solo manifest, senza service worker (nessuna cache dell'app, prompt di installazione non garantito su tutti i browser, e Web Push richiede comunque un service worker); cache anche dei dati per la lettura offline (titoli e trascrizioni salvati sul dispositivo, da cancellare a logout ed eliminazione dell'account, molto più complessa).

**Formato di registrazione**: solo il formato nativo senza normalizzazione (file non navigabili, bitrate diversi tra browser); codifica uniforme nel browser con un encoder WebAssembly (più codice, dipendenze e consumo di batteria).

**Aggiornamento dello stato**: Azure SignalR Service o Web PubSub. Il piano Free ha 20 connessioni e 20.000 messaggi al giorno, senza SLA; il piano Standard è fuori budget.

**Hosting**: Cloudflare Pages, Netlify, Vercel Hobby, Blob Storage come sito statico.
