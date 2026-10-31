using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Memoa.Functions.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// OpenTelemetry del worker verso Application Insights. Senza connection string (sviluppo locale)
    /// la telemetria non viene esportata.
    /// </summary>
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (string.IsNullOrEmpty(connectionString))
        {
            return builder;
        }

        builder.Services.AddOpenTelemetry()
            .UseFunctionsWorkerDefaults()
            .WithTracing(tracing => tracing
                .AddHttpClientInstrumentation(options => options.RecordException = true)
                .AddProcessor(new SensitiveDataProcessor()))
            .UseAzureMonitorExporter(options =>
            {
                options.ConnectionString = connectionString;
                // Application Insights ha l'autenticazione locale disattivata: si usa l'identità
                // user-assigned indicata da AZURE_CLIENT_ID.
                options.Credential = new DefaultAzureCredential();
            });

        return builder;
    }
}
