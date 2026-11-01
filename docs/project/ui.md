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
  - **Filtri**: `mat-chip-listbox` a scelta singola, in una riga che scorre in orizzontale.
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

- **Barra in basso**, in tutte le pagine, fissa e con il fondo delle liste: Home, Aggiungi (al centro) e Tag, solo icone. Aggiungi apre un menu con Registra, Scrivi e Carica; Carica apre direttamente la scelta dei file.
- Le pagine lasciano in fondo lo spazio della barra; i messaggi brevi compaiono sopra la barra.

---

## Homepage

- **Intestazione**: una sola riga, il campo di ricerca (stile Gmail o Keep, a pillola) con dentro, a destra, l'avatar del menu utente (iniziali su tinta tenue), con nome dell'account, Impostazioni, Informazioni ed Esci. Il nome dell'app compare solo come testo del campo vuoto ("Cerca in Memoa"). Stesso fondo della pagina, resta in alto scorrendo.
- **Filtri per tag**: sotto la ricerca (solo se almeno una nota ha un tag), una riga di chip che scorre in orizzontale: "Tutte" e i tag assegnati alle note, in ordine di nome. Il filtro scelto vale per la lista e per i risultati della ricerca (tra i 10 frammenti trovati) e resta tornando alla homepage; toccando di nuovo il tag scelto si torna a "Tutte". La gestione dei tag è nella pagina Tag, dalla barra di navigazione.
- **Ricerca**: la ricerca è per significato, anche tra lingue diverse, e parte con Invio. I risultati prendono il posto della lista: il numero di risultati e una riga per frammento trovato, nello stesso stile della lista, con icona del tipo, titolo, data e il testo del frammento (al massimo due righe, poi i puntini di sospensione); il tap apre la nota. Svuotando il campo, o con la ×, torna la lista. Stati: ricerca in corso, nessun risultato, errore con "Riprova".
- **Lista**: tutte le note, dalla più recente per data della nota (a parità di data, per titolo). Le note sono raggruppate con un'intestazione per gruppo: "Oggi", "Ieri", poi un gruppo per mese (es. "Gennaio 2026"). Ogni gruppo è un blocco bianco con una riga per nota, separate da divisori sottili. Ogni riga mostra a sinistra l'icona del tipo (microfono per l'audio, righe per il testo), poi il titolo e, sotto, un estratto dell'inizio del testo su una riga con i puntini di sospensione (o lo stato, se la nota non è pronta) e gli eventuali tag; a destra l'ora (oggi e ieri) o giorno e mese (es. "27 gen") e, per gli audio, la durata.
  - **Pronta**: apre la pagina del record.
  - **In elaborazione**: appare attenuata con l'etichetta "In elaborazione", ma si apre comunque.
  - **In errore**: appare attenuata con l'etichetta "Errore". Al tap si apre una finestra che spiega l'errore e offre "Riprova" (solo se il record si può riprovare; altrimenti dice che il file non è valido), "Elimina" (con la conferma dell'eliminazione) e "Chiudi".
- **Stati della pagina**: diario vuoto con invito a toccare +, nessuna nota con il tag scelto, caricamento, errore con "Riprova".
- **Aggiunta**: dalla voce + della barra di navigazione (vedi [Navigazione](#navigazione)).

---

## Nuova nota

- **Registra**: un pulsante primario con icona e testo ("Avvia registrazione", poi "Ferma e salva") avvia e ferma la registrazione (massimo 30 minuti); sopra, il tempo trascorso e la durata massima. Allo stop la nota si salva subito, con data e ora attuali e come titolo data e ora (es. "1 novembre 2026, 14:35"), e si torna alla homepage.
- **Scrivi**: titolo, data e ora e testo nella stessa pagina, con "Salva":
  - il titolo è prepopolato con la proposta da data e ora della nota (es. "TXT_20261101_1435"), che segue la data finché l'utente non lo modifica; se lo svuota si usa la proposta;
  - data e ora sono prepopolate con quelle attuali e modificabili al minuto;
  - testo con il contatore dei caratteri (massimo 50.000); "Salva" è attivo solo con del testo.
- **Carica**: più file insieme, ognuno diventa una nota.
  - Titolo dal nome del file senza estensione; data dall'ultima modifica del file (o attuale se manca).
  - I file si salvano uno alla volta, con lo stato di ciascuno: in attesa, avanzamento, salvato, non valido, non riuscito.
  - Un file non valido o non riuscito non ferma gli altri, e quelli non riusciti si possono riprovare.
  - Se tutti vanno a buon fine si torna alla homepage.
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
  - **Tag** (nel menu ⋮, solo per i record pronti): finestra con l'elenco dei tag da selezionare e un campo "Nuovo tag" per crearne uno (solo il nome), con Annulla e Salva. Un tag tolto qui non viene più assegnato in automatico alla nota.
  - **Modifica**: pulsante primario in fondo alla pagina, solo per i record pronti. Titolo, data e ora e, se c'è, testo; dopo una modifica del testo la nota viene indicizzata di nuovo.
- Dopo una modifica del testo o una rigenerazione si resta sulla pagina, che mostra l'avanzamento.

---

## Tag

- **Elenco** (`/tags`, dalla barra di navigazione): pulsante primario "Nuovo tag", poi un blocco bianco con una riga per tag in ordine di nome: nome, descrizione su una riga, numero di note ed eventuale "Disattivato", con accanto "Assegnazione in corso" (icona e testo) mentre il tag è in assegnazione; a destra l'interruttore dell'assegnazione automatica. Finché almeno un tag è in assegnazione la lista si aggiorna con il polling. Il tap sulla riga apre il tag. Stati: nessun tag (con invito a crearne uno), caricamento, errore con "Riprova".
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
