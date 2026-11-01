# Funzionalità

[← Indice del progetto](../project.md)

Funzionalità dell'MVP e vincoli su formati, dimensioni e lingue. Interfaccia in [ui.md](ui.md).

**Priorità**: **core** = necessaria per il primo rilascio · **avanzata** = rilasciabile in seguito.
**Stato**: ✅ implementata · 🟡 parziale · ⬜ da fare.

Contenuti: [Account](#account) · [Contenuti in ingresso](#contenuti-in-ingresso) · [Elaborazione](#elaborazione) · [Consultazione e gestione](#consultazione-e-gestione) · [Privacy](#privacy) · [Vincoli](#vincoli)

---

## Account

| Funzionalità | Descrizione | Priorità | Stato |
|---|---|---|---|
| Autenticazione | Registrazione, login e logout con Entra External ID. L'utente applicativo si crea al primo accesso. | core | ✅ |
| Lingua dell'account | Italiano o inglese, scelta alla registrazione e modificabile dalle impostazioni. Vale per interfaccia e riassunti, e fa da ripiego per la trascrizione. | core | 🟡 modificabile dalle impostazioni, ma alla registrazione vale l'italiano e l'interfaccia è solo in italiano |

---

## Contenuti in ingresso

| Funzionalità | Descrizione | Priorità | Stato |
|---|---|---|---|
| Registrazione audio | Registrazione dal browser, fino a 30 minuti. | core | ✅ |
| Upload audio | Caricamento di file audio, anche più insieme: ognuno diventa una nota. | core | ✅ |
| Scrittura testo | Inserimento diretto di testo. | core | ✅ |
| Upload testo | Caricamento di file di testo, anche più insieme. | core | 🟡 solo TXT e MD; DOCX e PDF da fare |
| Titolo, data e ora | Vedi [sotto](#titolo-data-e-ora). | core | ✅ |
| Conservazione dell'originale | L'audio caricato o registrato resta collegato al testo, accanto alla copia normalizzata in Opus. | core | ✅ |
| Testo da immagine | Immagine scritta a mano, con estrazione del testo (OCR/HTR). | avanzata | ⬜ |
| Upload video | Trascrizione della sola traccia audio. | avanzata | ⬜ |

### Titolo, data e ora

- Ogni nota ha un titolo (massimo 200 caratteri) e una data e ora, usate per l'ordine cronologico. Le calcola il client nel fuso orario dell'utente e si salvano in UTC.
- **Registrazione**: si salva subito, con data e ora attuali, e come titolo data e ora.
- **Upload**: si salva subito, con la data di ultima modifica del file (o quella attuale se manca), e come titolo il nome del file.
- **Scrittura**: titolo, data e ora si chiedono dopo il testo. Data e ora sono prepopolate e modificabili al minuto; senza titolo si usano data e ora della nota.
- Titolo, data e ora sono modificabili dalla pagina del record.

---

## Elaborazione

| Funzionalità | Descrizione | Priorità | Stato |
|---|---|---|---|
| Trascrizione | Audio → testo con Azure AI Speech, ripiego su Whisper ([trascrizione-ai.md](trascrizione-ai.md)). | core | ✅ |
| Riconoscimento della lingua | Italiano o inglese rilevato per ogni registrazione; se incerto vale la lingua dell'account. | core | ✅ |
| Stato di elaborazione | "In elaborazione", "pronto" o "in errore", aggiornato con polling; "riprova" in caso di errore. | core | ✅ |
| Rigenerazione della trascrizione | Nuova trascrizione automatica di un audio, che sostituisce il testo corrente. | core | ✅ |

---

## Consultazione e gestione

| Funzionalità | Descrizione | Priorità | Stato |
|---|---|---|---|
| Vista elenco | Record ordinati per data della nota e raggruppati in oggi, ieri e poi per mese, con titolo, data o ora, durata degli audio, stato e un estratto dell'inizio del testo. I record in elaborazione si aprono, quelli in errore offrono "riprova". La pagina del record mostra il testo e l'audio normalizzato riproducibile. | core | ✅ |
| Modifica ed eliminazione | Titolo, data e testo modificabili dalla pagina del record, inclusa la correzione della trascrizione (l'originale resta salvato). Eliminazione del record con tutti i suoi file. | core | ✅ |
| Ricerca semantica | Ricerca per significato, anche tra lingue diverse (es. query in inglese su note in italiano), dalla homepage: restituisce i frammenti più simili (al massimo 10), ciascuno con il collegamento alla nota. | core | ✅ |
| Domande sui record (Q&A/RAG) | Risposta in linguaggio naturale con rimando ai record di origine. | avanzata | ⬜ |
| Riassunti periodici | Riassunti generati periodicamente. | avanzata | ⬜ |
| Tag | Tag dell'utente con nome e descrizione (massimo 2.000 caratteri), gestiti dalla pagina Tag: modifica, eliminazione, elenco delle note e interruttore per disattivarli. Assegnati in automatico per similarità semantica tra tag e testo della nota, oppure a mano dalla pagina del record (anche creando un tag nuovo). Un tag tolto a mano non viene riassegnato in automatico. | avanzata | ✅ |
| Categorie | Categorie delle note. | avanzata | ⬜ |

---

## Privacy

| Funzionalità | Descrizione | Priorità | Stato |
|---|---|---|---|
| Informativa privacy | Informativa GDPR. Deve indicare, tra l'altro, che i file cancellati restano recuperabili per 14 giorni. | core | ⬜ |
| Esportazione dati | Esportazione di tutti i propri dati. | core | ⬜ |
| Eliminazione account | Cancellazione dell'account e di tutti i dati: database, tutti i contenitori blob e i messaggi in coda. Dalla pagina Informazioni, con conferma. | core | 🟡 database e blob eliminati, messaggi in coda ignorati dagli handler; l'utente in Entra External ID resta (da definire) |

---

## Vincoli

| Vincolo | Valore |
|---|---|
| Audio | MP3, M4A/AAC, WAV, FLAC, Ogg/Opus, WebM, AMR/3GP, verificati sul formato reale con `ffmpeg`; nessun video. Massimo 350 MB per file e 30 minuti. |
| Testo | Tutti i formati di testo, inclusi DOCX e PDF. Massimo 10 MB per file e 50.000 caratteri di testo. |
| Immagini | Da definire. |
| Lingue | Italiano e inglese. |
| Piattaforme | Web e mobile (PWA). |
