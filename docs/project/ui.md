# Interfaccia utente

[← Indice del progetto](../project.md)

Principi, schermate e comportamenti dell'interfaccia. Funzionalità e stato di implementazione in [funzionalita.md](funzionalita.md), aspetti tecnici del client in [frontend.md](frontend.md).

Contenuti: [Principi](#principi) · [Regole visive](#regole-visive) · [Navigazione](#navigazione) · [Homepage](#homepage) · [Nuova nota](#nuova-nota) · [Pagina del record](#pagina-del-record) · [Tag](#tag) · [Impostazioni](#impostazioni) · [Informazioni](#informazioni) · [Da fare](#da-fare)

---

## Principi

- **Mobile first**: progettata e verificata prima a larghezza telefono, poi adattata agli schermi più grandi.
- **Semplicità**: interfaccia essenziale, priorità alla facilità d'uso.
- **Lingua**: oggi solo italiano.

---

## Regole visive

Valgono per ogni schermata. Una nuova schermata o un nuovo componente le rispetta senza eccezioni; se una regola non basta, si aggiorna questa sezione prima di scrivere codice.

### Componenti

- **Angular Material** (Material 3) per tutti i componenti: pulsanti, card, liste, finestre, campi, menu, barre di avanzamento, snackbar. Niente componenti fatti a mano se Material ne ha uno equivalente.
- Personalizzazione tramite il tema (token di sistema e override dei componenti); poche regole globali in `styles.scss` solo dove i token non bastano (es. angoli dei campi pieni).
- **Un solo stile per ogni cosa**:
  - **Card**: `mat-card` con aspetto *outlined*, bianca sul fondo carta, unico tipo di card dell'app.
  - **Lista di note** (homepage e risultati della ricerca): un solo componente di riga dentro un blocco bianco con divisori sottili, a tutta larghezza su telefono e arrotondato sugli schermi grandi.
  - **Campi**: `mat-form-field` pieno (*fill*), angoli arrotondati, senza linea inferiore; il focus è un bordo nel colore d'accento.
  - **Pulsante primario**: `mat-flat-button` (pieno, colore d'accento), al massimo uno per schermata o finestra.
  - **Pulsante secondario**: `mat-button` (testo).
  - **Pulsante distruttivo** (es. Elimina): `mat-button` con testo esplicito; la conferma avviene sempre nella finestra di conferma.
  - **Azione di aggiunta**: una sola, la voce + della barra di navigazione in basso (nessun FAB).
  - **Navigazione principale**: barra in basso, in tutte le pagine, con sole icone (con `aria-label`); la voce attiva ha l'icona nel colore d'accento su una pillola in tinta tenue.
  - **Filtri**: in un pannello (`mat-card` *outlined*) che si apre con un'animazione da un'icona; scelte con `mat-chip-listbox` (a scelta singola o multipla), intervalli numerici con `mat-slider` a due cursori, date con l'input nativo di tipo data. I filtri attivi sono chip (`mat-chip` con la X di `matChipRemove`) sotto la ricerca.
  - **Finestra di conferma**: un solo componente condiviso basato su `MatDialog`, con titolo, testo, azione di conferma (a destra) e "Annulla" (a sinistra).
  - **Messaggi brevi** (salvato, errore temporaneo): `MatSnackBar`.
  - **Interruttore** (attiva/disattiva): `mat-slide-toggle`, fuori dal collegamento della riga e con `aria-label`.
  - **Tag di una nota**: `mat-chip-set` di chip non interattivi nella pagina del record; nelle righe delle liste una riga in stile Etichetta con icona e nomi separati da " · ".
  - **Scelta multipla in una finestra**: `mat-selection-list` dentro `MatDialog`.

### Spaziature

- Solo valori della scala **4, 8, 12, 16, 24, 32 px**, per margini, padding e distanze tra elementi.
- Definiti come variabili CSS (es. `--space-1` … `--space-6`); nessun valore in pixel scritto direttamente negli stili.
- Margine laterale della pagina su telefono: 16 px.

### Testo

- **Al massimo 4 dimensioni** in tutta l'app, prese dalla scala tipografica del tema:

  | Ruolo | Uso |
  |---|---|
  | Titolo | titolo della pagina |
  | Corpo | testo delle note, voci di lista, campi |
  | Secondario | date, conteggi, descrizioni |
  | Etichetta | etichette di stato, didascalie |

- La **gerarchia** si ottiene soprattutto con **peso** (regolare / medio) e **colore** (testo principale / testo attenuato), non con nuove dimensioni.
- Due famiglie, entrambe di sistema (nessun font scaricato): **serif** solo per i titoli di pagina (in grassetto), **sans** per tutto il resto.

### Colore

- **Un solo colore d'accento pieno**, il viola del logo, per le azioni principali (pulsante primario, elemento attivo, focus).
- **Tinte tenui** dell'accento ammesse per elementi secondari (es. avatar, sfondi leggeri), purché il contrasto del testo resti sufficiente.
- Il resto in **neutri caldi**: fondo della pagina color carta, superfici bianche per liste e card, campi in un grigio caldo, bordi sottili, testo principale e attenuato.
- Il **logo dell'app** (foglio con onda sonora, lo stesso della favicon) è a colori nel diario vuoto.
- Il colore di errore del tema si usa solo per gli errori, sempre insieme a testo e icona.
- Tema chiaro e scuro con gli stessi ruoli di colore: nessun colore scritto direttamente nei componenti, solo token del tema.

### Accessibilità

- **Contrasto** almeno WCAG AA, in entrambi i temi: 4,5:1 per il testo, 3:1 per icone, bordi dei campi e altri elementi dell'interfaccia.
- **Aree toccabili** di almeno **48 × 48 px** (mai sotto 44), anche per icone e voci di lista; densità predefinita di Material, non compatta.
- **Mai informazioni affidate solo al colore**: ogni stato (in elaborazione, errore, attenuato) ha anche un'etichetta di testo o un'icona.
- Focus da tastiera sempre visibile; icone senza testo con etichetta accessibile (`aria-label`).

---

## Navigazione

- **Barra in basso**, in tutte le pagine, fissa e con il fondo delle liste: Home, Aggiungi (al centro) e Tag, solo icone. Aggiungi espande in orizzontale, sopra la barra, una pillola con Scrivi, Registra e Carica (icona con una piccola etichetta sotto) e la sua croce ruota a X; si chiude con un secondo tocco, toccando fuori, con Esc o scegliendo un'azione. Carica apre direttamente la scelta dei file.
- Le pagine lasciano in fondo lo spazio della barra; i messaggi brevi compaiono sopra la barra.
- **Home e Tag affiancate**: passando dall'una all'altra (dalla barra o con lo scorrimento) la pagina scorre lateralmente, la Home a sinistra e i Tag a destra, mentre la barra in basso resta ferma (View Transitions del router; senza supporto del browser la pagina cambia subito, nessuna animazione con la riduzione del movimento attiva). Scorrendo col dito verso sinistra dalla Home si va ai Tag, verso destra dai Tag si torna alla Home. Sulle righe delle liste vince il gesto della riga: verso destra scopre il cestino, e una riga aperta si chiude; lo scorrimento non vale con la selezione multipla attiva, sopra finestre e menu, nei campi e nei cursori.

---

## Homepage

- **Intestazione**: una sola riga, il campo di ricerca (stile Gmail o Keep, a pillola) con dentro, a destra, l'avatar del menu utente (iniziali su tinta tenue), con nome dell'account, Impostazioni, Guida, Informazioni ed Esci. Il nome dell'app compare solo come testo del campo vuoto ("Cerca in Memoa"). Stesso fondo della pagina, resta in alto scorrendo.
- **Filtri**: a destra della riga del primo gruppo ("Oggi", "Ieri" o il mese), alla stessa altezza, l'icona dei filtri (nel colore d'accento su una tinta tenue con il pannello aperto o con filtri attivi). Il tocco apre e chiude, con una breve animazione, un pannello sotto la riga con:
  - **Tipologia**: Audio o Testo, a scelta singola (toccando di nuovo quella scelta la si toglie).
  - **Tag**: i tag attivi assegnati alle note, in ordine di nome, a scelta multipla; con più tag compaiono le note che ne hanno almeno uno. I tag disattivati non compaiono. I tag si filtrano solo da qui.
  - **Periodo**: Oggi, Ultimi 7 giorni (oggi compreso), Questo mese o Personalizzato, con data di inizio e di fine (entrambe incluse, ciascuna facoltativa).
  - **Durata degli audio**: slider a due cursori da 0 alla durata massima di un audio, al minuto; agli estremi non c'è limite. La durata riguarda solo gli audio: impostandola la tipologia passa ad Audio; se l'utente poi toglie la tipologia tornano anche i testi e la durata vale solo per gli audio (gli audio senza durata, non ancora preparati, restano fuori).
  - In fondo, "Cancella filtri" a sinistra (toglie le scelte del pannello, attivo solo se ce n'è almeno una) e "Applica" a destra.

  Le scelte nel pannello valgono solo toccando "Applica", che chiude il pannello; chiudendolo con l'icona le scelte non applicate si perdono. I filtri applicati valgono per la lista e per i risultati della ricerca (tra i 10 frammenti trovati), e restano tornando alla homepage. Quelli attivi compaiono come chip subito sotto la ricerca (es. "Audio", il nome di un tag, "Ultimi 7 giorni", "1 ott – 15 ott", "2–10 min"), ciascuna con la X per toglierlo. Se i filtri non lasciano note, l'icona resta sopra il messaggio "Nessuna nota con questi filtri". La gestione dei tag è nella pagina Tag, dalla barra di navigazione.
- **Ricerca**: la ricerca è per significato, anche tra lingue diverse, e parte con Invio. I risultati prendono il posto della lista: il numero di risultati e una riga per frammento trovato, nello stesso stile della lista, con icona del tipo, titolo, data e il testo del frammento (al massimo due righe, poi i puntini di sospensione); il tap apre la nota. Svuotando il campo, o con la ×, torna la lista. Stati: ricerca in corso, nessun risultato, errore con "Riprova".
- **Lista**: tutte le note, dalla più recente per data della nota (a parità di data, per titolo). Le note sono raggruppate con un'intestazione per gruppo: "Oggi", "Ieri", poi un gruppo per mese (es. "Gennaio 2026"). Ogni gruppo è un blocco bianco con una riga per nota, separate da divisori sottili. Ogni riga mostra a sinistra l'icona del tipo (microfono per l'audio, righe per il testo), poi il titolo e, sotto, un estratto dell'inizio del testo su una riga con i puntini di sospensione (o lo stato, se la nota non è pronta) e gli eventuali tag; a destra l'ora (oggi e ieri) o giorno e mese (es. "27 gen") e, per gli audio, la durata.
  - **Pronta**: apre la pagina del record.
  - **In elaborazione**: appare attenuata con l'etichetta "In elaborazione", ma si apre comunque.
  - **In errore**: appare attenuata con l'etichetta "Errore". Al tap si apre una finestra che spiega l'errore e offre "Riprova" (solo se il record si può riprovare; altrimenti dice che il file non è valido), "Elimina" (con la conferma dell'eliminazione) e "Chiudi".
  - **Eliminazione**: premendo e scorrendo la riga verso destra si scopre a sinistra il cestino, rosso su una tinta tenue dello stesso rosso; rilasciata oltre metà dello spazio la riga resta aperta (una alla volta), altrimenti torna al suo posto. Il cestino chiede la conferma dell'eliminazione. La riga aperta si chiude scorrendola verso sinistra, toccandola, toccando un punto qualsiasi fuori dalla riga o con Esc.
  - **Selezione multipla**: la pressione prolungata su una nota la seleziona; con la selezione attiva un tocco aggiunge o toglie una nota (senza aprirla) e lo scorrimento per eliminare è disattivato. Le note selezionate hanno tutte lo stesso sfondo, quello del passaggio del mouse (sul telefono l'ultima nota toccata resta in hover e non deve distinguersi dalle altre). Al posto della ricerca l'intestazione mostra la X per annullare, il numero delle note selezionate, il pulsante dei tag e il cestino (con conferma); il resto della pagina è coperto da uno strato trasparente e toccandolo, o con Esc, la selezione si chiude.
  - **Tag di più note**: il pulsante dei tag apre la stessa finestra della pagina del record (titolo "Tag di N note"), con spuntati i tag comuni a tutte le note selezionate. Al salvataggio i tag spuntati vengono aggiunti a tutte le note e quelli comuni tolti vengono tolti da tutte; gli altri tag di ciascuna nota restano. Come per la singola nota, i tag che restano su una nota diventano manuali (anche quelli assegnati in automatico). Le note salvate si aggiornano subito nella lista; se alcune non si salvano la finestra resta aperta con il numero di quelle non riuscite e Salva riprova solo quelle. Dopo il salvataggio la selezione si chiude.
- **Stati della pagina**: diario vuoto con invito a toccare +, nessuna nota con i filtri scelti, caricamento, errore con "Riprova".
- **Aggiunta**: dalla voce + della barra di navigazione (vedi [Navigazione](#navigazione)).

---

## Nuova nota

- **Registra**: un pulsante primario con icona e testo ("Avvia registrazione", poi "Ferma e salva") avvia e ferma la registrazione (massimo 30 minuti); sopra, il tempo trascorso e la durata massima. Allo stop la nota si salva subito, con data e ora attuali e come titolo data e ora (es. "1 novembre 2026, 14:35"), e si torna alla homepage.
- **Scrivi**: titolo, data e ora e testo nella stessa pagina, con "Salva":
  - il titolo è prepopolato con la proposta da data e ora della nota (es. "TXT_20261101_1435"), che segue la data finché l'utente non lo modifica; se lo svuota si usa la proposta;
  - data e ora sono prepopolate con quelle attuali e modificabili al minuto;
  - testo con il contatore dei caratteri (massimo 50.000); "Salva" è attivo solo con del testo.
- **Carica**: più file insieme, ognuno diventa una nota.
  - Prima del salvataggio un'anteprima, una card per file: titolo (prepopolato con il nome del file senza estensione; se lo si svuota si usa la proposta REC/TXT dalla data) e data e ora (prepopolate con l'ultima modifica del file, o attuali se manca), entrambi modificabili. I file non validi mostrano il motivo e non vengono salvati. "Salva" avvia gli upload, "Annulla" scarta l'anteprima.
  - I file si salvano uno alla volta, con lo stato di ciascuno: in attesa, avanzamento (rotellina e percentuale), salvato, non riuscito. Ogni file in attesa, in caricamento (solo audio) o non riuscito ha la X per toglierlo; un audio annullato in corso non lascia la nota.
  - Un file non riuscito non ferma gli altri, e quelli non riusciti si possono riprovare.
  - Gli upload continuano cambiando pagina (l'anteprima non confermata no); chiudendo l'app si perdono. In homepage, sotto le chip dei filtri attivi, una chip ("Caricamento di N file", o "N caricamenti non riusciti") riporta alla pagina di upload.
  - Se tutti vanno a buon fine, con la pagina aperta, si torna alla homepage.
- **Errore di salvataggio**: "Riprova" senza ripetere la registrazione o la scrittura. Per il testo c'è anche "Modifica".

---

## Pagina del record

- **Intestazione**: titolo, freccia per tornare alle note e menu ⋮ con Cerca nel testo, Modifica, Tag, Rigenera trascrizione ed Elimina. Sotto, data e ora della nota ed eventuale stato ("in elaborazione", "errore"), poi i tag come chip (con un'icona per quelli assegnati in automatico).
- **Tag automatici**: dopo il passaggio a "pronto" la pagina si ricarica una volta dopo qualche secondo, per mostrare i tag assegnati in automatico.
- **Audio**: lettore della copia normalizzata, disponibile appena esiste.
- **Testo**: per una trascrizione, un'etichetta dice "Trascrizione automatica: potrebbe contenere errori" oppure "…, modificata da te".
- **Elaborazione**: elenco dei passi (Preparazione dell'audio, Trascrizione, Indicizzazione per la ricerca) con quello in corso. La pagina si aggiorna con il polling finché è aperta e visibile.
  - Durante la prima trascrizione un segnaposto prende il posto del testo.
  - Durante una rigenerazione un avviso dice che la nuova trascrizione sostituirà il testo.
- **Azioni**:
  - **Elimina** (nel menu ⋮): sempre disponibile, con conferma che cita i 14 giorni di recupero dei file.
  - **Rigenera trascrizione** (nel menu ⋮): per gli audio, con conferma che avvisa se le modifiche al testo andranno perse.
  - **Tag** (nel menu ⋮, solo per i record pronti): finestra con l'elenco dei tag da selezionare e un campo "Nuovo tag" per crearne uno (solo il nome), con Annulla e Salva. Un tag tolto qui non viene più assegnato in automatico alla nota. La stessa finestra si usa per più note dalla selezione multipla della homepage.
  - **Modifica**: pulsante primario in fondo alla pagina, solo per i record pronti. Titolo, data e ora e, se c'è, testo; dopo una modifica del testo la nota viene indicizzata di nuovo.
- Dopo una modifica del testo o una rigenerazione si resta sulla pagina, che mostra l'avanzamento.

---

## Guida

- **Proposta**: all'apertura della homepage, una volta per sessione, finché l'utente non ha completato né rifiutato la guida: una finestra ("Vuoi fare un giro di Memoa?") con "No, grazie" e "Inizia il tour". "No, grazie", Esc o un tocco fuori valgono come rifiuto.
- **Tour**: la pagina viene oscurata tranne l'elemento del passo, con accanto (sotto se c'è spazio, altrimenti sopra; su telefono largo quanto lo schermo meno i margini) un fumetto con "N di 7", la X, titolo, testo e i pulsanti Salta, Indietro e Avanti ("Fine" all'ultimo passo). Passi: benvenuto (al centro), + della barra, lista delle note, ricerca, filtri (con il diario vuoto l'icona compare solo durante questo passo), Tag della barra, menu utente. Esc vale come Salta, le frecce cambiano passo; la pagina sotto non si tocca.
- **Fine o rifiuto**: "Fine", "Salta", la X ed Esc chiudono la guida per sempre (salvato nell'account). Uscendo dalla homepage durante il tour la guida si chiude senza salvare e viene proposta al prossimo accesso.
- **Menu utente → Guida**: avvia il tour senza la proposta, anche dopo averlo chiuso.

---

## Tag

- **Elenco** (`/tags`, dalla barra di navigazione): pulsante primario "Nuovo tag", poi un blocco bianco con una riga per tag in ordine di nome: nome, descrizione su una riga, numero di note ed eventuale "Disattivato", con accanto "Assegnazione in corso" (icona e testo) mentre il tag è in assegnazione; a destra l'interruttore dell'assegnazione automatica. Finché almeno un tag è in assegnazione la lista si aggiorna con il polling. Il tap sulla riga apre il tag. Stati: nessun tag (con invito a crearne uno), caricamento, errore con "Riprova".
  - **Eliminazione e selezione multipla**: come nella lista delle note. Scorrendo la riga verso destra si scopre il cestino (con la stessa conferma della pagina del tag); la pressione prolungata seleziona un tag e l'intestazione mostra la X, il numero dei tag selezionati e il cestino (con conferma, "Eliminare N tag?"). Con la selezione attiva l'interruttore è disattivato e il tap sulla riga aggiunge o toglie il tag dalla selezione.
- **Tag** (`/tags/:tagUid`, e `/tags/new` per crearne uno): campi Nome (massimo 50 caratteri) e Descrizione (massimo 2.000, con contatore e un suggerimento su a cosa serve), pulsante primario Salva (o Crea), menu ⋮ con Elimina (con conferma: il tag viene tolto da tutte le note, le note restano). Sotto, mentre il tag è in assegnazione, "Assegnazione alle note in corso" (icona e testo), poi le note con il tag, nello stesso stile della lista. Finché il tag è in assegnazione stato e note si aggiornano con il polling, senza toccare i campi.
- Dopo la creazione o una modifica un messaggio breve conferma il salvataggio; l'avanzamento lo mostra lo stato del tag.

---

## Impostazioni

- **Lingua preferita**: italiano o inglese, salvata nell'account.
- **Aspetto**: sistema, chiaro o scuro. Vale solo per il dispositivo ed è salvato nel browser.

---

## Informazioni

- **Account**: nome ed email dell'account.
- **Elimina account**: pulsante distruttivo con descrizione dei dati eliminati (note, registrazioni audio, trascrizioni, impostazioni). La conferma avvisa che verranno eliminati tutti i dati, che l'operazione non si può annullare e che i file restano recuperabili per 14 giorni. Durante l'eliminazione il pulsante è disattivato; poi l'app esce dall'account. In caso di errore, un messaggio invita a riprovare.

---

## Da fare

- Nome visualizzato (display name) nelle impostazioni: previsto, non implementato.
- Interfaccia in inglese.
- Esportazione dati e informativa privacy (vedi [funzionalita.md](funzionalita.md)), probabilmente nella pagina Informazioni.
