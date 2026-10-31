using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Storage;

/// <summary>
/// Firma gli URL di upload diretto nel contenitore degli originali. Lo storage account accetta solo
/// Entra ID, quindi i SAS sono user delegation SAS firmati con una chiave ottenuta dall'identità delle
/// Functions (ruolo Storage Blob Delegator), tenuta in cache e rinnovata prima della scadenza.
/// </summary>
public sealed class UploadSasService(BlobServiceClient blobServiceClient, IOptions<ContainerOptions> containers)
{
    /// <summary>Durata del SAS: deve coprire l'upload a blocchi di un file grande su una rete lenta.</summary>
    public static readonly TimeSpan SasLifetime = TimeSpan.FromMinutes(30);

    // Margine per orologi non allineati tra Functions e storage.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    // La chiave vale al massimo 7 giorni; un giorno limita la finestra se l'identità viene revocata.
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromDays(1);

    private readonly SemaphoreSlim _keyLock = new(1, 1);
    private UserDelegationKey? _key;

    /// <summary>
    /// Restituisce un URL valido solo per creare il blob indicato: il permesso Create non permette di
    /// sovrascrivere un blob esistente, né di leggerlo, elencarlo o cancellarlo.
    /// </summary>
    public async Task<(Uri Uri, DateTimeOffset ExpiresOn)> CreateUploadUriAsync(
        string blobName, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresOn = now + SasLifetime;
        var key = await GetKeyAsync(expiresOn, cancellationToken);

        var sas = new BlobSasBuilder
        {
            BlobContainerName = containers.Value.Uploads,
            BlobName = blobName,
            Resource = "b",
            StartsOn = now - ClockSkew,
            ExpiresOn = expiresOn,
            Protocol = SasProtocol.Https,
        };
        sas.SetPermissions(BlobSasPermissions.Create);

        var blob = blobServiceClient.GetBlobContainerClient(containers.Value.Uploads).GetBlobClient(blobName);
        var uri = new BlobUriBuilder(blob.Uri)
        {
            Sas = sas.ToSasQueryParameters(key, blobServiceClient.AccountName),
        };

        return (uri.ToUri(), expiresOn);
    }

    private async Task<UserDelegationKey> GetKeyAsync(DateTimeOffset sasExpiresOn, CancellationToken cancellationToken)
    {
        // Il SAS non può scadere dopo la chiave che lo firma.
        if (_key is { } cached && cached.SignedExpiresOn >= sasExpiresOn)
        {
            return cached;
        }

        await _keyLock.WaitAsync(cancellationToken);
        try
        {
            if (_key is { } current && current.SignedExpiresOn >= sasExpiresOn)
            {
                return current;
            }

            var now = DateTimeOffset.UtcNow;
            var response = await blobServiceClient.GetUserDelegationKeyAsync(
                now - ClockSkew, now + KeyLifetime, cancellationToken);
            _key = response.Value;
            return _key;
        }
        finally
        {
            _keyLock.Release();
        }
    }
}
