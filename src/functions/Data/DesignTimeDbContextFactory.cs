using Memoa.Functions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Memoa.Functions.Data;

/// <summary>
/// Usata solo da "dotnet ef" per generare migration e script: non apre connessioni, quindi la
/// connection string è fittizia e il database non viene risvegliato.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MemoaDbContext>
{
    public MemoaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MemoaDbContext>()
            .UseAzureSql("Server=design-time;Database=memoa")
            .Options;

        return new MemoaDbContext(options, new CurrentUser());
    }
}
