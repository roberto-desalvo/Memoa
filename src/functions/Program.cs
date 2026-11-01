using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Embeddings;
using Memoa.Functions.Normalization;
using Memoa.Functions.Observability;
using Memoa.Functions.Storage;
using Memoa.Functions.Transcription;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.AddObservability();

builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<TokenValidator>();
builder.UseMiddleware<AuthenticationMiddleware>();
// Dopo l'autenticazione: usa l'utente validato.
builder.UseMiddleware<UserProvisioningMiddleware>();
// Eliminazione dell'account di accesso in Entra External ID: rimandata, per ora l'account resta.
builder.Services.AddSingleton<IIdentityAccountService, RetainedIdentityAccountService>();

builder.Services.AddOptions<DataStorageOptions>()
    .Bind(builder.Configuration.GetSection(DataStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<ContainerOptions>()
    .Bind(builder.Configuration.GetSection(ContainerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
// In Azure DefaultAzureCredential usa l'identità user-assigned indicata da AZURE_CLIENT_ID.
builder.Services.AddSingleton(sp => new BlobServiceClient(
    sp.GetRequiredService<IOptions<DataStorageOptions>>().Value.BlobServiceUri,
    new DefaultAzureCredential()));
builder.Services.AddOptions<QueueOptions>()
    .Bind(builder.Configuration.GetSection(QueueOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
// Base64 come i binding delle code, così i trigger leggono anche i messaggi scritti da questo client.
builder.Services.AddSingleton(sp => new QueueServiceClient(
    sp.GetRequiredService<IOptions<DataStorageOptions>>().Value.QueueServiceUri,
    new DefaultAzureCredential(),
    new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }));
builder.Services.AddSingleton<BlobSasService>();
builder.Services.AddSingleton<UploadExpiryScheduler>();
builder.Services.AddSingleton<RecordBlobService>();

builder.Services.AddOptions<FfmpegOptions>()
    .Bind(builder.Configuration.GetSection(FfmpegOptions.SectionName));
builder.Services.AddSingleton<Ffmpeg>();

builder.Services.AddOptions<OpenAIOptions>()
    .Bind(builder.Configuration.GetSection(OpenAIOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
    return new AzureOpenAIClient(options.Endpoint, new DefaultAzureCredential())
        .GetEmbeddingClient(options.EmbeddingDeployment);
});

builder.Services.AddOptions<SpeechOptions>()
    .Bind(builder.Configuration.GetSection(SpeechOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
// Unico punto in cui si sceglie il servizio di trascrizione. Il timeout lo decide TranscribeRecord in base
// alla durata dell'audio, non HttpClient (100 secondi di predefinito).
builder.Services.AddHttpClient<ITranscriber, AzureSpeechTranscriber>(client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddScoped<TranscriptStore>();

builder.AddDatabase();

builder.Build().Run();
