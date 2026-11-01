using Memoa.Functions.Authentication;
using Microsoft.Data.SqlClient;
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
            var connectionString = WithConnectTimeout(sp.GetRequiredService<IOptions<SqlOptions>>().Value.ConnectionString);

            // I retry coprono anche la ripresa del database serverless dopo la pausa automatica
            // (errore 40613). Con i retry attivi, le transazioni esplicite vanno eseguite dentro
            // Database.CreateExecutionStrategy().ExecuteAsync(...).
            options.UseAzureSql(connectionString, sql => sql.EnableRetryOnFailure());

            // I timeout durante la ripresa li ritenta l'apertura della connessione.
            options.AddInterceptors(new SqlConnectionRetryInterceptor());

            // Mai EnableSensitiveDataLogging: esporrebbe nei log i valori dei parametri (testi,
            // trascrizioni, email).
        });

        // Il testo dei comandi SQL non contiene dati (le query sono parametrizzate), ma riempirebbe
        // la quota giornaliera di Application Insights.
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

        return builder;
    }

    /// <summary>
    /// Alza a 30 secondi il timeout di connessione (predefinito 15), se la stringa di connessione non
    /// ne indica uno più lungo: la ripresa del database serverless richiede spesso più di 15 secondi.
    /// </summary>
    private static string WithConnectTimeout(string connectionString)
    {
        const int MinConnectTimeoutSeconds = 30;

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (builder.ConnectTimeout >= MinConnectTimeoutSeconds)
        {
            return connectionString;
        }

        builder.ConnectTimeout = MinConnectTimeoutSeconds;
        return builder.ConnectionString;
    }
}
