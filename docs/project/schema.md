# Schema del database

[← Indice del progetto](../project.md)

Tabelle di Azure SQL Database (EF Core 10). Scelte su database e migration in [dati.md](dati.md). Il codice di riferimento è in `src/functions/Data` (entità in `Entities`, configurazioni in `Configurations`).

Contenuti: [Convenzioni](#convenzioni) · [Users](#users) · [Records](#records) · [Attachments](#attachments) · [RecordChunks](#recordchunks) · [Tags](#tags) · [RecordTags](#recordtags) · [Relazioni e indici](#relazioni-e-indici)

---

## Convenzioni

- **Chiavi primarie** `int identity`, solo interne al database. Verso l'esterno (blob, code, log, client) si usa solo `RecordUid`.
- **Enum** salvati come stringhe (varchar). **Date** in UTC (datetime2): `CreatedAt` all'inserimento, `UpdatedAt` a ogni salvataggio.
- **Cancellazione fisica** con cascata, nessun soft delete nel database. I file su Blob Storage vanno cancellati a parte.
- **Filtro globale** `CurrentUserFilter` su `Records`, `Attachments`, `RecordChunks`, `Tags` e `RecordTags`: le query restituiscono solo i dati dell'utente autenticato. Le Functions attivate da code ed Event Grid non hanno un utente: usano `IgnoreQueryFilters([CurrentUserFilter])` e filtrano per record. Il filtro non si applica a SQL scritto a mano.
- **Dati personali** (`Email`, `Title`, `Text`, `OriginalTranscript`, `Content`, `Name` e `Description` dei tag): mai nei log, sempre inclusi in esportazione e cancellazione.

---

## Users

Utente applicativo, creato alla prima richiesta autenticata.

| Colonna | Tipo | Note |
|---|---|---|
| Id | int identity, PK | |
| EntraObjectId | uniqueidentifier, univoco | Claim `oid`. Ogni ambiente ha il proprio tenant e il proprio database. |
| Email | nvarchar(320) | Dal claim opzionale `email`. Aggiornata se cambia, mai svuotata se il claim manca. |
| PreferredLanguage | varchar(5) | `it` / `en`, `it` alla creazione. |
| CreatedAt | datetime2 | |

---

## Records

Record del diario, comune a tutte le origini.

| Colonna | Tipo | Note |
|---|---|---|
| Id | int identity, PK | |
| RecordUid | uniqueidentifier, univoco | Identificativo pubblico, GUID v7 generato nel codice. Compare nei percorsi dei blob. |
| UserId | int, FK → Users | |
| Title | nvarchar(200) | |
| SourceType | varchar(20) | `Audio` / `Text` / `Image` (`Image` non ancora usato). |
| Status | varchar(20) | `Processing` / `Ready` / `Error`. |
| ErrorCode | varchar(50), null | Solo un codice, mai contenuti. |
| Text | nvarchar(max), null | Testo scritto o trascrizione corrente, modificabile. Se cambia, i chunk vanno ricalcolati. Candidata a un futuro indice full-text. |
| OriginalTranscript | nvarchar(max), null | Trascrizione automatica, mai modificata dall'utente. Null per i record di testo e durante una rigenerazione. |
| Language | varchar(5), null | Lingua rilevata. |
| OccurredAt | datetime2 | Data e ora della nota ([funzionalita.md](funzionalita.md#titolo-data-e-ora)). Ordine della lista: `OccurredAt`, poi `Title`. |
| CreatedAt, UpdatedAt | datetime2 | |
| RowVersion | rowversion | Concorrenza ottimistica tra modifiche dell'utente e pipeline. |

---

## Attachments

File collegati a un record (audio, in futuro immagini). Le colonne specifiche di un tipo sono nullable.

| Colonna | Tipo | Note |
|---|---|---|
| Id | int identity, PK | |
| RecordId | int, FK → Records | |
| Kind | varchar(20) | `Audio` / `Image`. |
| OriginalBlobPath | nvarchar(1024) | `<oid>/<RecordUid>` nel contenitore `uploads`, senza estensione. Mai cancellato né sovrascritto. |
| OriginalContentType | varchar(100) | |
| OriginalSizeBytes | bigint | |
| NormalizedBlobPath | nvarchar(1024), null | Copia Opus in `audio`, riprodotta nella pagina del record. |
| DurationSeconds | int, null | Dai metadati della copia normalizzata, salvata da `EnqueueTranscription` (`NormalizeRecord` non scrive nel database). |
| CreatedAt | datetime2 | |

---

## RecordChunks

Frammenti del testo di un record con il relativo embedding ([trascrizione-ai.md](trascrizione-ai.md#embedding)). Il testo si divide perché 50.000 caratteri superano gli 8.191 token accettati dal modello.

| Colonna | Tipo | Note |
|---|---|---|
| Id | int identity, PK | |
| RecordId | int, FK → Records | |
| ChunkIndex | int | Posizione del frammento nel testo. |
| Content | nvarchar(max) | Testo del frammento, per mostrare il punto trovato. |
| Embedding | vector(1536) | `SqlVector<float>`, circa 6 KB. Nessun indice vettoriale: ricerca esatta con `VECTOR_DISTANCE`, risultati da raggruppare per record. |
| EmbeddingModel | varchar(100) | Modello restituito dal servizio: cambiarlo richiede di ricalcolare tutti i vettori. |
| CreatedAt | datetime2 | |

I chunk esistono solo dopo un'indicizzazione completata: `IndexRecord` li sostituisce tutti in una transazione, insieme allo stato "pronto".

---

## Tags

Tag dell'utente ([backend-elaborazione.md](backend-elaborazione.md#tag)).

| Colonna | Tipo | Note |
|---|---|---|
| Id | int identity, PK | |
| TagUid | uniqueidentifier, univoco | Identificativo pubblico, GUID v7. |
| UserId | int, FK → Users | |
| Name | nvarchar(50) | Univoco per utente. |
| Description | nvarchar(2000), null | Usata con il nome per l'embedding. |
| IsActive | bit | Se falso il tag non viene più assegnato in automatico. |
| Embedding | vector(1536) | Embedding di nome e descrizione, ricalcolato quando cambiano. |
| EmbeddingModel | varchar(100) | Si confronta solo con chunk dello stesso modello. |
| CreatedAt, UpdatedAt | datetime2 | |
| RowVersion | rowversion | |

---

## RecordTags

Assegnazione di un tag a un record, oppure la sua esclusione.

| Colonna | Tipo | Note |
|---|---|---|
| RecordId | int, FK → Records, PK | |
| TagId | int, FK → Tags, PK | |
| Source | varchar(20) | `Auto` (per similarità, ricalcolata) / `Manual` (scelta dall'utente) / `Excluded` (tolta dall'utente, non riassegnata). |
| Similarity | float, null | Solo per `Auto`. |
| CreatedAt | datetime2 | |

---

## Relazioni e indici

| Elemento | Definizione |
|---|---|
| Users → Records | 1 → N, cascata |
| Records → Attachments | 1 → N, cascata |
| Records → RecordChunks | 1 → N, cascata |
| Users → Tags | 1 → N, cascata |
| Records → RecordTags | 1 → N, cascata |
| Tags → RecordTags | 1 → N, **senza cascata** (SQL Server non accetta due percorsi di cascata da Users): le righe si eliminano prima del tag e prima dell'utente |
| Indice univoco | `Users (EntraObjectId)` |
| Indice univoco | `Records (RecordUid)`: ricerca dalla pipeline, inserimento idempotente |
| Indice | `Records (UserId, OccurredAt)`: vista elenco |
| Indice | `Attachments (RecordId)`, per convenzione sulla chiave esterna |
| Indice univoco | `RecordChunks (RecordId, ChunkIndex)`: nessun duplicato se due indicizzazioni si sovrappongono |
| Indice univoco | `Tags (TagUid)`, `Tags (UserId, Name)` |
| Chiave primaria | `RecordTags (RecordId, TagId)`, più l'indice `RecordTags (TagId)` |
