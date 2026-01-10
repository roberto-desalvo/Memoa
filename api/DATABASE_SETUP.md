# Database Setup - EF Core + Supabase/Postgres con Migrations

## Prerequisiti

- Supabase project attivo oppure Postgres locale
- .NET 9 SDK installato

## Configurazione

### 1. Connection String

Aggiorna `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<host>;Port=5432;Database=memoa;Username=<user>;Password=<password>;"
  }
}
```

**Per Supabase:**
- Host: da Supabase dashboard > Project Settings > Database
- Database: postgres (default)
- Username: postgres (default)
- Password: la password impostata al setup

**Per Postgres locale:**
```
DefaultConnection: "Host=localhost;Port=5432;Database=memoa;Username=postgres;Password=postgres;"
```

### 2. Creare il Database

Accedi al tuo Supabase/Postgres e crea il database:

```bash
psql -h <host> -U postgres -d postgres -c "CREATE DATABASE memoa;"
```

### 3. Applicare le Migrations

Le migrations vengono applicate automaticamente all'avvio dell'app in Development.

**Se vuoi applicarle manualmente:**

```bash
cd api/Memoa.WebApi
dotnet ef database update --project .\Memoa.Domain\ --startup-project .\Memoa.WebApi\
```

**Per creare una nuova migration:**
```bash
dotnet ef migrations add <MigrationName> --project .\Memoa.Domain\ --startup-project .\Memoa.WebApi\
```

### 4. Avviare l'Applicazione

```bash
cd api/Memoa.WebApi
dotnet restore
dotnet build
dotnet run --project .\Memoa.WebApi\
```

Al primo avvio in Development, le migrations vengono applicate automaticamente.

## Testing

Usa Postman/HTTP REST Client per testare:

**POST /api/v1/entries** (protetto - richiede JWT)
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "text": "Mia prima entry",
  "createdAt": "2026-01-10T10:30:00Z"
}
```

**GET /api/v1/entries** (protetto - richiede JWT)
```
Ritorna tutte le entries dell'utente autenticato
```

## Troubleshooting

### Errore di connessione
- Verifica la connection string in appsettings.json
- Assicurati che il database sia raggiungibile
- Per Supabase, controlla il firewall

### Migration non applicata
- Verifica che le migrations siano nella cartella `Memoa.Domain/Migrations`
- Controlla che il DbContext sia correttamente configurato
- Prova a riavviare l'app

### Errore JWT
- L'header `Authorization: Bearer <token>` è obbligatorio
- Il token deve essere valido per il Supabase project configurato

## Struttura delle Migrations

Le migrations si trovano in `Memoa.Domain/Migrations/`:
- `20260110000000_InitialCreate.cs` - Crea la tabella Entries
- `MemoaDbContextModelSnapshot.cs` - Snapshot dello schema EF Core

## Aggiungere una nuova Migration

1. Modifica il DbContext o aggiungi un nuovo Entity
2. Esegui il comando per creare la migration
3. Riavvia l'app e la migration verrà applicata automaticamente

