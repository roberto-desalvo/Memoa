using Memoa.Functions.Authentication;
using Memoa.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoa.Functions.Data;

public sealed class MemoaDbContext(DbContextOptions<MemoaDbContext> options, CurrentUser currentUser)
    : DbContext(options)
{
    /// <summary>
    /// Nome del filtro globale che limita record, allegati e chunk all'utente autenticato. Senza un
    /// utente (trigger di code ed Event Grid) il filtro non restituisce nulla: quelle funzioni usano
    /// <c>IgnoreQueryFilters([CurrentUserFilter])</c> e filtrano esplicitamente per record.
    /// Il filtro non si applica a SQL scritto a mano.
    /// </summary>
    public const string CurrentUserFilter = "CurrentUser";

    public DbSet<User> Users => Set<User>();

    public DbSet<Record> Records => Set<Record>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<RecordChunk> RecordChunks => Set<RecordChunk>();

    // Il filtro deve riferirsi a un membro del contesto per essere valutato a ogni query.
    private Guid? CurrentEntraObjectId => currentUser.EntraObjectId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MemoaDbContext).Assembly);

        modelBuilder.Entity<Record>().HasQueryFilter(CurrentUserFilter,
            r => r.User.EntraObjectId == CurrentEntraObjectId);
        modelBuilder.Entity<Attachment>().HasQueryFilter(CurrentUserFilter,
            a => a.Record.User.EntraObjectId == CurrentEntraObjectId);
        modelBuilder.Entity<RecordChunk>().HasQueryFilter(CurrentUserFilter,
            c => c.Record.User.EntraObjectId == CurrentEntraObjectId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SetTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>CreatedAt all'inserimento, UpdatedAt a ogni salvataggio (UTC).</summary>
    private void SetTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedAt") is not null)
            {
                entry.Property("CreatedAt").CurrentValue = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified
                && entry.Metadata.FindProperty("UpdatedAt") is not null)
            {
                entry.Property("UpdatedAt").CurrentValue = now;
            }
        }
    }
}
