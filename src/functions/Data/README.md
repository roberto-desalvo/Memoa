## Database e migration

Lo schema è gestito con EF Core Migrations, nel progetto Functions (`src/functions/Data/Migrations`). In dev e prod le migration le applica la pipeline dello schema come script SQL idempotente. Mai all'avvio delle Functions. I comandi seguenti servono per lavorare in locale sul database dev su Azure (non esiste un database locale).

Prerequisiti:

- login con l'account amministratore Entra del server SQL, l'unico con i permessi per modificare lo schema (l'identità delle Functions ha solo lettura e scrittura dei dati):
  ```powershell
  az login
  ```
- `Sql__ConnectionString` compilata in `src/functions/local.settings.json` (`Authentication=Active Directory Default`).
- nella sessione PowerShell, limitare le credenziali alla sola Azure CLI:
  ```powershell
  $env:AZURE_TOKEN_CREDENTIALS = "AzureCliCredential"
  ```
  `Active Directory Default` prova più credenziali in ordine, e Visual Studio e VS Code vengono prima della CLI. Se uno dei due è collegato a un altro account o tenant, il token arriva da lì e il server lo rifiuta con `Login failed for user '<token-identified principal>'. The server is not currently configured to accept this token.` La variabile è la stessa che `local.settings.json` imposta per le Functions, ma `dotnet ef` non legge quel file.

### Creare una migration

```powershell
dotnet ef migrations add <NomeMigration> --project src/functions --output-dir Data/Migrations
```

Non si connette al database. Ogni modifica va fatta in due tempi: prima le aggiunte compatibili, poi le rimozioni o le rinomine in un rilascio successivo. Il codice deve funzionare sia con lo schema vecchio sia con quello nuovo.

Per annullare l'ultima migration non ancora applicata:

```powershell
dotnet ef migrations remove --project src/functions
```

### Applicare le migration al database dev

**Opzione consigliata: script idempotente**, lo stesso formato usato dalla pipeline. Puoi rileggerlo prima di applicarlo e rieseguirlo senza effetti.

```powershell
# Genera lo script (non si connette al database)
dotnet ef migrations script --idempotent --project src/functions -o "$env:TEMP\memoa-schema.sql"

# Applica lo script con go-sqlcmd (una volta: winget install sqlcmd)
sqlcmd -S "<server>.database.windows.net" -d "sqldb-memoa-dev" `
  --authentication-method ActiveDirectoryDefault `
  -i "$env:TEMP\memoa-schema.sql"
```

**Alternativa rapida:** applica direttamente con EF Core, usando la connection string di `local.settings.json`. La factory di design-time ha una connection string fittizia, quindi `--connection` è obbligatorio.

```powershell
$cs = (Get-Content src/functions/local.settings.json -Raw | ConvertFrom-Json).Values.Sql__ConnectionString
dotnet ef database update --project src/functions --connection $cs
```

Per tornare a una migration precedente (solo in dev): `dotnet ef database update <NomeMigration> --project src/functions --connection $cs`.

Altri comandi utili:

```powershell
# Elenco delle migration e di quelle già applicate
dotnet ef migrations list --project src/functions --connection $cs
```

Ogni comando che si connette risveglia il database serverless e consuma la quota gratuita di vCore-secondi. Chiudi gli strumenti di query (SSMS, Azure Data Studio, estensione mssql) quando hai finito: le connessioni aperte impediscono la pausa automatica.
