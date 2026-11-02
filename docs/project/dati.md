# Dati

[← Indice del progetto](../project.md)

Database, accesso ai dati, migration, storage dei file e backup. Tabelle e colonne sono in [schema.md](schema.md).

Contenuti: [Database](#database) · [Accesso ai dati e migration](#accesso-ai-dati-e-migration) · [Storage dei file](#storage-dei-file) · [Backup](#backup) · [Appendice: alternative scartate](#appendice-alternative-scartate)

---

## Database

**Decisione**: Azure SQL Database con l'offerta gratuita (serverless), un database per ambiente (`sqldb-memoa-dev`, `sqldb-memoa-prod`).

- **Offerta gratuita**: fino a 10 database per sottoscrizione, ciascuno con 100.000 vCore-secondi al mese e 32 GB. Tutti nella stessa regione, fissata dal primo.
- **Configurazione**: GP_S_Gen5, da 0,5 a 2 vCore, pausa automatica dopo 60 minuti. A quota esaurita il database va in pausa (`AutoPause`) invece di generare costi.
- **Server**: esistente e condiviso, non gestito dal template. L'amministratore Entra ID e la regola firewall per i servizi Azure sono già configurati. Gli utenti del database per le identità gestite si creano con T-SQL dopo il deploy ([infra/README.md](../../infra/README.md)).
- **Ricerca vettoriale**: tipo `VECTOR`, ricerca esatta con `VECTOR_DISTANCE`, adeguata a questi volumi. Niente indici vettoriali (DiskANN) né `VECTOR_SEARCH`, ancora sperimentali. L'indice richiede almeno 100 righe con vettori e la disponibilità in West Europe è da verificare.
- **Sviluppo locale**: sul database dev su Azure, nessun database locale. Consuma la quota del database dev. Le connessioni lasciate aperte dagli strumenti di query impediscono la pausa automatica.

---

## Accesso ai dati e migration

**Decisione**: EF Core 10. Schema gestito con EF Core Migrations, applicate come script SQL idempotente da una pipeline GitHub Actions dedicata.

- **EF Core 10**: LTS, supportata fino a novembre 2028. Supporta il tipo `VECTOR` (proprietà `SqlVector<float>`, colonna `vector(1536)`) e `VECTOR_DISTANCE`.
- **Pipeline dello schema** (`schema.yml`): parte dalle modifiche in `src/functions/Data/Migrations`. Push su `main` → dev, push su `prod` → prod. Genera lo script con `dotnet ef migrations script --idempotent`, senza connettersi al database, e lo salva tra gli artifact, così si può rileggere prima di approvare prod. Lo applica con `sqlcmd` usando l'identità di deploy, che è un utente del database con i permessi di modifica dello schema.
- **Mai all'avvio delle Functions**: su Flex Consumption più istanze eseguirebbero la stessa migration in parallelo, e l'identità delle Functions avrebbe bisogno dei permessi di modifica dello schema.
- **Modifiche in due tempi**: schema e codice si distribuiscono separatamente, e i messaggi in coda possono essere elaborati dal codice precedente. Prima le aggiunte compatibili, poi le rimozioni o rinomine in un rilascio successivo. Il codice deve funzionare con entrambi gli schemi.
- Il deploy risveglia il database per pochi secondi: costo trascurabile.

---

## Storage dei file

**Decisione**: Azure Blob Storage, storage account dedicato ai dati utente (blob e code), separato da quello di sistema delle Functions. Isola accessi, regole di ciclo di vita e cancellazioni GDPR, senza costi in più.

- **Contenitori**: `uploads` (originali audio), `audio` (copie normalizzate ed esiti), `transcripts` (solo ripiego), `documents` (file di testo originali: PDF, MD, TXT; allegato di tipo `Document` del record di testo). Dettagli in [backend-elaborazione.md](backend-elaborazione.md#componenti).
- **Accesso**: solo Entra ID (`allowSharedKeyAccess: false`), nessun accesso pubblico. Ogni identità ha i ruoli minimi sui contenitori e sulle code che usa.
- **SAS**: user delegation SAS firmati dalle Functions (ruolo Storage Blob Delegator):
  - upload: permesso solo `Create` su un blob, valido 30 minuti; non permette di sovrascrivere, leggere o cancellare;
  - riproduzione: solo lettura della copia normalizzata, valido 2 ore, firmato da `GetRecordAudio` dopo aver verificato che il record appartenga all'utente;
  - download del file di testo originale: solo lettura, valido 15 minuti, con `Content-Disposition: attachment`, firmato da `GetRecordOriginal`.
- **CORS**: PUT, GET, HEAD e OPTIONS solo dalle origini del frontend.
- **Costo**: nessun piano gratuito permanente, ma trascurabile. Hot circa 0,02 $/GB al mese, circa il doppio con GRS. La copia Opus occupa circa 0,25 MB al minuto, mentre gli originali possono arrivare a 350 MB.
- **Ciclo di vita**: gli originali audio passano al tier Cold dopo 1 giorno (si rileggono solo per rinormalizzare); le copie normalizzate e i file di testo originali (al più 1 MB, scaricabili dall'utente) al tier Cool dopo 30 giorni (`audioCoolAfterDays`).
- **Trascrizioni del ripiego**: conservazione dopo il salvataggio nel database da definire (per ora nessuna regola).

---

## Backup

**Decisione**:
- **Database**: backup automatici di Azure SQL, con 32 GB di spazio inclusi nell'offerta gratuita e ridondanza locale. Export esterno (.bacpac) da valutare in seguito.
- **File**: soft delete di blob e contenitori per 14 giorni, versioning, versioni precedenti eliminate dopo 30 giorni, ridondanza GRS, blocco `CanNotDelete` sullo storage account.

Note:
- **Cosa sta dove**: testi, trascrizioni ed embedding stanno nel database; gli audio su Blob Storage.
- **GRS**: replica in modo asincrono nella regione abbinata (North Europe, quindi in UE). Protegge da guasti dell'intera regione, ma replica anche le cancellazioni: da quelle proteggono soft delete e versioning.
- **Informativa privacy**: va indicato che i file cancellati restano recuperabili per 14 giorni.
- **Attenzione**: i backup del database sono a ridondanza locale (`requestedBackupStorageRedundancy: 'Local'` in `sql.bicep`), mentre i file sono GRS. Un guasto dell'intera regione farebbe perdere i backup del database, ma non i file. A questi volumi è accettabile; da rivalutare insieme all'export esterno.

---

## Appendice: alternative scartate

**Database**: Azure Cosmos DB for NoSQL (free tier, ricerca vettoriale integrata), Postgres con pgvector fuori da Azure.

**Accesso ai dati**: Dapper, ADO.NET.

**Gestione dello schema**: DbUp o grate (SQL scritto a mano, nessuna generazione automatica); DACPAC (modello state-based, rischio di diff distruttivi sulle rinomine; Visual Studio 2026 non supporta i progetti SDK-style); Flyway e Liquibase (basati su Java; Liquibase con licenza FSL dalla versione 5.0).

**Formato delle migration**: migration bundle (eseguibile), meno rileggibile dello script SQL.

**Storage**: object storage esterno (es. Cloudflare R2).

**Backup dei file**: copie nel tier Archive, replica in un secondo storage account.
