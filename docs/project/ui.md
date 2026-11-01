# Interfaccia utente

[← Indice del progetto](../project.md)

Principi, schermate e comportamenti dell'interfaccia. Funzionalità e stato di implementazione in [funzionalita.md](funzionalita.md), aspetti tecnici del client in [frontend.md](frontend.md).

Contenuti: [Principi](#principi) · [Homepage](#homepage) · [Nuova nota](#nuova-nota) · [Pagina del record](#pagina-del-record) · [Impostazioni](#impostazioni) · [Da fare](#da-fare)

---

## Principi

- **Mobile first**: progettata e verificata prima a larghezza telefono, poi adattata agli schermi più grandi.
- **Semplicità**: interfaccia essenziale, priorità alla facilità d'uso.
- **Lingua**: oggi solo italiano.

---

## Homepage

- **Intestazione**: logo e menu utente (avatar con le iniziali), con nome dell'account, Impostazioni ed Esci.
- **Lista**: tutte le note, dalla più recente per data della nota (a parità di data, per titolo). Ogni voce mostra icona del tipo (audio o testo), titolo e data, più il numero totale di note.
  - **Pronta**: apre la pagina del record.
  - **In elaborazione**: appare attenuata con l'etichetta "In elaborazione", ma si apre comunque.
  - **In errore**: appare attenuata con l'etichetta "Errore". Al tap si apre una finestra che spiega l'errore e offre "Riprova" (solo se il record si può riprovare; altrimenti dice che il file non è valido), "Elimina" e "Chiudi".
- **Stati della pagina**: diario vuoto con invito a toccare +, caricamento, errore con "Riprova".
- **Aggiunta**: pulsante + in basso. Al tap apre una barra con Registra, Scrivi e Carica; Carica apre direttamente la scelta dei file.

---

## Nuova nota

- **Registra**: un pulsante avvia e ferma la registrazione (massimo 30 minuti). Allo stop la nota si salva subito, con data e ora attuali e come titolo data e ora (es. "1 novembre 2026, 14:35"), e si torna alla homepage.
- **Scrivi**: prima il testo (con il contatore dei caratteri, massimo 50.000), poi "Avanti" porta a titolo, data e ora:
  - data e ora sono prepopolate e modificabili al minuto;
  - se il titolo resta vuoto, si usano data e ora della nota.
- **Carica**: più file insieme, ognuno diventa una nota.
  - Titolo dal nome del file senza estensione; data dall'ultima modifica del file (o attuale se manca).
  - I file si salvano uno alla volta, con lo stato di ciascuno: in attesa, avanzamento, salvato, non valido, non riuscito.
  - Un file non valido o non riuscito non ferma gli altri, e quelli non riusciti si possono riprovare.
  - Se tutti vanno a buon fine si torna alla homepage.
- **Errore di salvataggio**: "Riprova" senza ripetere la registrazione o la scrittura. Per il testo c'è anche "Modifica".

---

## Pagina del record

- **Intestazione**: titolo e freccia per tornare alle note. Sotto, data e ora della nota ed eventuale stato ("in elaborazione", "errore").
- **Audio**: lettore della copia normalizzata, disponibile appena esiste.
- **Testo**: per una trascrizione, un'etichetta dice "Trascrizione automatica: potrebbe contenere errori" oppure "…, modificata da te".
- **Elaborazione**: elenco dei passi (Preparazione dell'audio, Trascrizione, Indicizzazione per la ricerca) con quello in corso. La pagina si aggiorna con il polling finché è aperta e visibile.
  - Durante la prima trascrizione un segnaposto prende il posto del testo.
  - Durante una rigenerazione un avviso dice che la nuova trascrizione sostituirà il testo.
- **Azioni**:
  - **Elimina**: sempre disponibile, con conferma che cita i 14 giorni di recupero dei file.
  - **Rigenera trascrizione**: per gli audio, con conferma che avvisa se le modifiche al testo andranno perse.
  - **Modifica**: solo per i record pronti. Titolo, data e ora e, se c'è, testo; dopo una modifica del testo la nota viene indicizzata di nuovo.
- Dopo una modifica del testo o una rigenerazione si resta sulla pagina, che mostra l'avanzamento.

---

## Impostazioni

- **Lingua preferita**: italiano o inglese, salvata nell'account.
- **Aspetto**: sistema, chiaro o scuro. Vale solo per il dispositivo ed è salvato nel browser.

---

## Da fare

- Nome visualizzato (display name) nelle impostazioni: previsto, non implementato.
- Interfaccia in inglese.
- Ricerca, esportazione dati, eliminazione account, informativa privacy (vedi [funzionalita.md](funzionalita.md)).
