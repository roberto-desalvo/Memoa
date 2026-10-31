# Schema del database

Struttura del database relazionale (Azure SQL Database, accesso tramite EF Core 10). Le scelte generali su database, accesso ai dati e migration sono in `claude/project.md`. Prima versione dello schema, soggetta a modifiche.

Convenzioni: chiavi primarie GUID (uniqueidentifier, generate nel codice; EF Core genera di default GUID sequenziali per SQL Server). Enum salvati come stringhe (`HasConversion<string>()`). Date in UTC (datetime2). Cancellazione fisica (hard delete) con cascata; nessun soft delete nel database.

---

## Users

Utente applicativo, collegato all'account di Entra External ID.

| Colonna | Tipo | Note |
|---|---|---|
| Id | uniqueidentifier, PK | |
| EntraObjectId | uniqueidentifier, univoco | Claim `oid` del token. I tenant sono due (dev e prod), ma ogni ambiente ha il proprio database |
| Email | nvarchar(320) | Copia dell'email dell'account Entra External ID; la sincronizzazione con Entra in caso di modifica sarà definita in futuro. Dato personale da includere in esportazione e cancellazione; mai nei log |
| PreferredLanguage | varchar(5) | `it` / `en` |
| CreatedAt | datetime2 | |

## Records

Record del diario, comune a tutte le origini.

| Colonna | Tipo | Note |
|---|---|---|
| Id | uniqueidentifier, PK | Generato nel codice prima del salvataggio: la Function HTTP lo usa anche nel percorso del blob |
| UserId | uniqueidentifier, FK → Users | Filtro globale di EF Core sull'utente corrente (isolamento applicativo). Il filtro si disattiva con `IgnoreQueryFilters` e non si applica a SQL scritto a mano. Le Functions attivate da eventi e code non hanno un utente corrente nel token |
| SourceType | varchar(20) | `Audio` / `Text` / `Image` |
| Status | varchar(20) | `Processing` / `Ready` / `Error` |
| ErrorCode | varchar(50), null | Solo un codice, mai contenuti |
| Text | nvarchar(max), null | Testo scritto o trascrizione corrente, modificabile dall'utente. Dopo una modifica gli embedding (RecordChunks) vanno ricalcolati. Colonna candidata per un futuro indice full-text (ricerca ibrida) |
| OriginalTranscript | nvarchar(max), null | Trascrizione automatica, mai modificata; permette di ripristinarla e di valutare la qualità della trascrizione. Null per i record di testo. Dato personale da includere in esportazione e cancellazione |
| Language | varchar(5), null | Lingua rilevata |
| OccurredAt | datetime2 | Data della nota, usata per l'ordine cronologico |
| CreatedAt | datetime2 | |
| UpdatedAt | datetime2 | |
| RowVersion | rowversion | Concorrenza ottimistica tra modifiche dell'utente e Function di indicizzazione |

## Attachments

File collegati a un record (audio, in futuro immagini). Tabella unica per tutti i tipi: le colonne specifiche di un tipo sono nullable.

| Colonna | Tipo | Note |
|---|---|---|
| Id | uniqueidentifier, PK | |
| RecordId | uniqueidentifier, FK → Records | |
| Kind | varchar(20) | `Audio` / `Image` |
| OriginalBlobPath | nvarchar(1024) | File caricato o registrato, mai cancellato né sovrascritto |
| OriginalContentType | varchar(100) | |
| OriginalSizeBytes | bigint | |
| NormalizedBlobPath | nvarchar(1024), null | Copia normalizzata in Opus (solo audio) |
| DurationSeconds | int, null | Solo audio. Il job di normalizzazione non scrive nel database: il valore va scritto da una Function |
| CreatedAt | datetime2 | |

## RecordChunks

Frammenti del testo di un record con il relativo embedding. text-embedding-3-small accetta al massimo 8.191 token in ingresso: i 50.000 caratteri consentiti per il testo (circa 12-15.000 token in italiano) li superano e una registrazione di 30 minuti è al limite, quindi il testo viene suddiviso.

| Colonna | Tipo | Note |
|---|---|---|
| Id | uniqueidentifier, PK | |
| RecordId | uniqueidentifier, FK → Records | La ricerca semantica interroga questa tabella con VECTOR_DISTANCE: il filtro globale definito su Records non si applica alle query dirette su RecordChunks. I risultati vanno raggruppati per record |
| ChunkIndex | int | Posizione del frammento nel testo |
| Content | nvarchar(max) | Testo del frammento, utile per mostrare il punto trovato |
| Embedding | vector(1536) | `SqlVector<float>` in EF Core 10. Circa 6 KB per vettore |
| EmbeddingModel | varchar(100) | Nome del modello: cambiarlo richiede di ricalcolare tutti i vettori |
| CreatedAt | datetime2 | |

---

## Relazioni e indici

| Elemento | Definizione | Note |
|---|---|---|
| Users → Records | 1 → N, cancellazione a cascata | Base dell'eliminazione dell'account |
| Records → Attachments | 1 → N, cancellazione a cascata | La cascata elimina solo le righe: i file su Blob Storage vanno cancellati a parte e restano recuperabili per 14 giorni (soft delete dello storage) |
| Records → RecordChunks | 1 → N, cancellazione a cascata | Consegna almeno una volta: la Function di indicizzazione deve sostituire tutti i chunk del record in una transazione, non aggiungerli |
| Indice univoco | Users (EntraObjectId) | |
| Indice | Records (UserId, OccurredAt) | Vista elenco |
| Indice | Attachments (RecordId) | |
| Indice univoco | RecordChunks (RecordId, ChunkIndex) | |