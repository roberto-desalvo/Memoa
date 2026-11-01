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
| Titolo, data e ora della nota | Registrazione e upload si salvano subito; titolo e data si modificano poi nella pagina del record. Registrazione: data e ora attuali, titolo uguale a data e ora (es. "1 novembre 2026, 14:35"). Upload: data e ora di ultima modifica del file (o quelle attuali se il browser non la fornisce), titolo uguale al nome del file senza estensione. Titolo di massimo 200 caratteri; data e ora calcolate dal client nel proprio fuso orario e inviate in UTC. Per la scrittura titolo, data e ora vengono chiesti dopo il testo: data e ora prepopolate con quelle attuali e modificabili al minuto; se il titolo non è specificato il client usa data e ora della nota nel fuso orario dell'utente. Per gli audio titolo e data vengono inviati insieme alla richiesta del SAS di upload |
| Upload di più file | Si possono scegliere più file insieme: ognuno diventa una nota separata e i file vengono salvati uno alla volta, con lo stato di ciascuno (in attesa, avanzamento, salvato, non valido, non riuscito). Un file non valido o non salvato non ferma gli altri; i file non riusciti si possono riprovare. Se vengono salvati tutti si torna alla homepage |
| Pagina del record | Un record pronto è cliccabile nella lista e apre la propria pagina, con l'audio normalizzato (Opus) riproducibile e il testo. Mostra data e ora della nota. Le operazioni sul record (modifica di titolo, data e ora e, se presente, del testo; eliminazione) si trovano in questa pagina. L'audio si riproduce tramite un SAS di sola lettura richiesto alla Function HTTP |
| Eliminazione di un record | Richiede conferma in una modale che cita la conservazione dei file cancellati per 14 giorni |
| Menu utente | Dà accesso alle impostazioni |
| Impostazioni | Lingua preferita (italiano / inglese), nome visualizzato (display name), modalità chiara / scura |