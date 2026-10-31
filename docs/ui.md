# Interfaccia utente

Principi e struttura dell'interfaccia della PWA (Angular). Le funzionalità esposte all'utente sono elencate in `claude/project.md` (sezioni 1-5), le scelte tecniche nella sezione 6. Prima versione, soggetta a modifiche.

---

## Principi

| Principio | Descrizione |
|---|---|
| Mobile first | Interfaccia progettata prima per lo smartphone, poi adattata agli schermi più grandi |
| Usabilità | Priorità alla facilità d'uso |
| Semplicità | Interfaccia essenziale |

## Struttura e navigazione

| Elemento | Descrizione |
|---|---|
| Homepage | Schermata di atterraggio all'apertura dell'app, con la lista dei propri record ordinati per data della nota (`OccurredAt`) e, a parità di data, per titolo. Ogni record è mostrato con il proprio titolo. I record non ancora pronti (in elaborazione o in errore) appaiono disabilitati |
| Record in errore | Pur apparendo disabilitato, al tap su un record in errore si apre una piccola modale che spiega che c'è stato un errore nell'elaborazione e chiede se si vuole riprovare, con il pulsante "riprova" |
| Aggiunta di un record | Icona di aggiunta in basso nella homepage. Al tap, dall'icona si apre una barra con le modalità di aggiunta, ognuna con la propria icona: registrazione, scrittura, upload |
| Titolo e data della nota | Chiesti dopo la registrazione, la scrittura o la scelta del file, per tutte le modalità. Titolo: massimo 200 caratteri, prepopolato con il nome del file per gli upload; se non specificato il client usa la data della nota nel fuso orario dell'utente. Data della nota: prepopolata con la data attuale (calcolata dal client nel proprio fuso orario e inviata in UTC), modificabile dall'utente, che può scegliere solo la data (il valore salvato è data e ora). Per gli audio titolo e data vengono inviati insieme alla richiesta del SAS di upload |
| Pagina del record | Un record pronto è cliccabile nella lista e apre la propria pagina, con l'audio normalizzato (Opus) riproducibile e il testo. Le operazioni sul record (modifica ed eliminazione) si trovano in questa pagina. L'audio si riproduce tramite un SAS di sola lettura richiesto alla Function HTTP |
| Eliminazione di un record | Richiede conferma in una modale che cita la conservazione dei file cancellati per 14 giorni |
| Menu utente | Dà accesso alle impostazioni |
| Impostazioni | Lingua preferita (italiano / inglese), nome visualizzato (display name), modalità chiara / scura |