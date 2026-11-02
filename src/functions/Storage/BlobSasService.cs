using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Storage;

/// <summary>
/// Firma gli URL di accesso diretto ai blob dal client: upload nei contenitori degli originali (audio e
/// file di testo), riproduzione dell'audio normalizzato e download dei file di testo. Lo storage account accetta solo
/// Entra ID, quindi i SAS sono user delegation SAS firmati con una chiave ottenuta dall'identità delle
/// Functions (ruolo Storage Blob Delegator), tenuta in cache e rinnovata prima della scadenza.
/// </summary>
public sealed class BlobSasService(BlobServiceClient blobServiceClient, IOptions<ContainerOptions> containers)
{
    /// <summary>Durata del SAS di upload: deve coprire l'upload a blocchi di un file grande su una rete lenta.</summary>
    public static readonly TimeSpan UploadSasLifetime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Durata del SAS di lettura: il player scarica l'audio a intervalli (richieste Range), quindi deve
    /// coprire l'ascolto di una registrazione di 30 minuti con delle pause.
    /// </summary>
    public static readonly TimeSpan ReadSasLifetime = TimeSpan.FromHours(2);

    /// <summary>Il download parte subito dopo la richiesta: basta il tempo di scaricare un file da 1 MB.</summary>
    public static readonly TimeSpan DownloadSasLifetime = TimeSpan.FromMinutes(15);

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
    public Task<(Uri Uri, DateTimeOffset ExpiresOn)> CreateUploadUriAsync(
        string blobName, CancellationToken cancellationToken) =>
        CreateUriAsync(containers.Value.Uploads, blobName, BlobSasPermissions.Create, UploadSasLifetime, cancellationToken);

    /// <summary>Restituisce un URL valido solo per leggere la copia normalizzata indicata.</summary>
    public Task<(Uri Uri, DateTimeOffset ExpiresOn)> CreateAudioReadUriAsync(
        string blobName, CancellationToken cancellationToken) =>
        CreateUriAsync(containers.Value.Audio, blobName, BlobSasPermissions.Read, ReadSasLifetime, cancellationToken);

    /// <summary>Come <see cref="CreateUploadUriAsync"/>, nel contenitore dei file di testo.</summary>
    public Task<(Uri Uri, DateTimeOffset ExpiresOn)> CreateDocumentUploadUriAsync(
        string blobName, CancellationToken cancellationToken) =>
        CreateUriAsync(containers.Value.Documents, blobName, BlobSasPermissions.Create, UploadSasLifetime, cancellationToken);

    /// <summary>
    /// Restituisce un URL valido solo per scaricare il file di testo originale, con il nome indicato: la
    /// risposta dello storage lo propone come download (Content-Disposition) con il tipo salvato.
    /// </summary>
    public Task<(Uri Uri, DateTimeOffset ExpiresOn)> CreateDocumentDownloadUriAsync(
        string blobName, string fileName, string contentType, CancellationToken cancellationToken) =>
        CreateUriAsync(containers.Value.Documents, blobName, BlobSasPermissions.Read, DownloadSasLifetime, cancellationToken,
            sas =>
            {
                sas.ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
                sas.ContentType = contentType;
            });

    private async Task<(Uri Uri, DateTimeOffset ExpiresOn)> CreateUriAsync(
        string containerName,
        string blobName,
        BlobSasPermissions permissions,
        TimeSpan lifetime,
        CancellationToken cancellationToken,
        Action<BlobSasBuilder>? configure = null)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresOn = now + lifetime;
        var key = await GetKeyAsync(expiresOn, cancellationToken);

        var sas = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = now - ClockSkew,
            ExpiresOn = expiresOn,
            Protocol = SasProtocol.Https,
        };
        sas.SetPermissions(permissions);
        configure?.Invoke(sas);

        var blob = blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
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
