## Database e migration

Lo schema è gestito con EF Core Migrations, in `Data/Migrations`. In dev e prod le migration le applica la pipeline dello schema come script SQL idempotente. Mai all'avvio delle Functions.

Comandi per creare e applicare le migration in locale: vedi [docs/sviluppo-locale.md](../../../docs/sviluppo-locale.md#database-e-migration).
