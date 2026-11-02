using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Memoa.Transcriber;
using Memoa.Transcriber.Audio;
using Memoa.Transcriber.Engine;
using Memoa.Transcriber.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Container Apps Job: ogni esecuzione elabora un messaggio e termina. Il codice di uscita diverso da
// zero segnala l'errore a Container Apps; il messaggio resta in coda per il tentativo successivo.
var builder = Host.CreateApplicationBuilder(args);

builder.AddObservability();

builder.Services.AddOptions<DataStorageOptions>()
    .Bind(builder.Configuration.GetSection(DataStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<JobOptions>()
    .Bind(builder.Configuration.GetSection(JobOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// In Azure DefaultAzureCredential usa l'identità user-assigned indicata da AZURE_CLIENT_ID.
var credential = new DefaultAzureCredential();
builder.Services.AddSingleton(sp => new BlobServiceClient(
    sp.GetRequiredService<IOptions<DataStorageOptions>>().Value.BlobServiceUri,
    credential));
// Le Functions scrivono i messaggi in base64 (codifica predefinita dell'output binding).
builder.Services.AddSingleton(sp => new QueueServiceClient(
        sp.GetRequiredService<IOptions<DataStorageOptions>>().Value.QueueServiceUri,
        credential,
        new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 })
    .GetQueueClient(sp.GetRequiredService<IOptions<JobOptions>>().Value.Queue));
builder.Services.AddSingleton<Ffmpeg>();

// Unico punto in cui si sceglie il motore di trascrizione, da Job__Engine (impostato dal target del
// Dockerfile). Si validano solo le opzioni del motore scelto: l'immagine contiene solo i suoi modelli.
var engine = builder.Configuration[$"{JobOptions.SectionName}:{nameof(JobOptions.Engine)}"];
if (engine == JobOptions.ParakeetEngineName)
{
    builder.Services.AddOptions<ParakeetOptions>()
        .Bind(builder.Configuration.GetSection(ParakeetOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddSingleton<ITranscriptionEngine, ParakeetEngine>();
}
else if (engine == JobOptions.FasterWhisperEngineName)
{
    builder.Services.AddOptions<FasterWhisperOptions>()
        .Bind(builder.Configuration.GetSection(FasterWhisperOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddSingleton<ITranscriptionEngine, FasterWhisperEngine>();
}
else
{
    builder.Services.AddOptions<WhisperOptions>()
        .Bind(builder.Configuration.GetSection(WhisperOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddSingleton<ITranscriptionEngine, WhisperEngine>();
}
builder.Services.AddSingleton<TranscriptionJob>();

using var host = builder.Build();
await host.StartAsync();

var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var exitCode = 0;

try
{
    // ApplicationStopping scatta con il SIGTERM inviato da Container Apps allo scadere del replicaTimeout.
    await host.Services.GetRequiredService<TranscriptionJob>().RunAsync(lifetime.ApplicationStopping);
}
catch (Exception ex)
{
    logger.LogError(ex, "Trascrizione non riuscita");
    exitCode = 1;
}

// Arresta l'host per inviare la telemetria in sospeso prima di uscire.
await host.StopAsync();
return exitCode;
