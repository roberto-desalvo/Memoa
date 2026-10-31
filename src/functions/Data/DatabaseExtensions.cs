using Memoa.Functions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Data;

public static class DatabaseExtensions
{
    /// <summary>
    /// Registra <see cref="MemoaDbContext"/> (scoped). Lo schema non viene mai creato né migrato qui:
    /// le migration le applica la pipeline dello schema.
    /// </summary>
    public static IHostApplicationBuilder AddDatabase(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<SqlOptions>()
            .Bind(builder.Configuration.GetSection(SqlOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddScoped<CurrentUser>();
        builder.Services.AddDbContext<MemoaDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IOptions<SqlOptions>>().Value.ConnectionString;

            // I retry coprono anche la ripresa del database serverless dopo la pausa automatica
            // (errore 40613). Con i retry attivi, le transazioni esplicite vanno eseguite dentro
            // Database.CreateExecutionStrategy().ExecuteAsync(...).
            options.UseAzureSql(connectionString, sql => sql.EnableRetryOnFailure());

            // Mai EnableSensitiveDataLogging: esporrebbe nei log i valori dei parametri (testi,
            // trascrizioni, email).
        });

        // Il testo dei comandi SQL non contiene dati (le query sono parametrizzate), ma riempirebbe
        // la quota giornaliera di Application Insights.
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

        return builder;
    }
}
