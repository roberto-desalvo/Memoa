using Azure.Identity;
using Azure.Storage.Blobs;
using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Observability;
using Memoa.Functions.Storage;
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
builder.Services.AddSingleton<UploadSasService>();

builder.AddDatabase();

builder.Build().Run();
