using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Memoa.Functions.Data.Entities;
using Memoa.Shared.Pipeline;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Storage;

/// <summary>Blob di un record nei quattro contenitori, tutti sotto il prefisso "&lt;oid&gt;/".</summary>
public sealed class RecordBlobService(BlobServiceClient blobServiceClient, IOptions<ContainerOptions> containers)
{
    /// <summary>
    /// Percorso dell'originale nel contenitore degli originali. Prefisso per utente: la cancellazione GDPR
    /// elimina tutti i suoi file con un solo prefisso. Nessuna estensione: il formato reale viene rilevato
    /// con ffmpeg durante la normalizzazione.
    /// </summary>
    public static string UploadBlobName(Guid entraObjectId, Guid recordUid) => $"{entraObjectId:D}/{recordUid:D}";

    /// <summary>
    /// True se il percorso ha la forma di <see cref="UploadBlobName"/>, la stessa nei contenitori degli
    /// originali audio e dei file di testo.
    /// </summary>
    public static bool TryParseUploadBlobName(string blobName, out Guid entraObjectId, out Guid recordUid)
    {
        entraObjectId = recordUid = Guid.Empty;
        var parts = blobName.Split('/');

        return parts.Length == 2
            && Guid.TryParseExact(parts[0], "D", out entraObjectId)
            && Guid.TryParseExact(parts[1], "D", out recordUid);
    }

    /// <summary>URL dell'originale, senza SAS: è il messaggio della coda di normalizzazione.</summary>
    public string UploadBlobUrl(string originalBlobPath) =>
        Container(containers.Value.Uploads).GetBlobClient(originalBlobPath).Uri.ToString();

    /// <summary>URL del file di testo originale, senza SAS: è il messaggio della coda di estrazione del testo.</summary>
    public string DocumentBlobUrl(string originalBlobPath) =>
        Container(containers.Value.Documents).GetBlobClient(originalBlobPath).Uri.ToString();

    /// <summary>URL della copia normalizzata, senza SAS: va nel messaggio della coda di trascrizione.</summary>
    public string NormalizedBlobUrl(string normalizedBlobPath) =>
        Container(containers.Value.Audio).GetBlobClient(normalizedBlobPath).Uri.ToString();

    /// <summary>
    /// True se il client ha completato l'upload dell'originale: nel contenitore degli originali audio per i
    /// record audio, in quello dei file di testo per gli altri.
    /// </summary>
    public async Task<bool> UploadExistsAsync(
        Guid entraObjectId, Guid recordUid, SourceType sourceType, CancellationToken cancellationToken) =>
        await Container(sourceType == SourceType.Audio ? containers.Value.Uploads : containers.Value.Documents)
            .GetBlobClient(UploadBlobName(entraObjectId, recordUid))
            .ExistsAsync(cancellationToken);

    /// <summary>
    /// Elimina i blob prodotti dalla pipeline (copia normalizzata, trascrizione e relativi esiti): i job
    /// saltano i record che hanno già un esito, quindi senza questi blob li rielaborano. L'originale resta.
    /// </summary>
    public Task DeleteDerivedAsync(Guid entraObjectId, Guid recordUid, CancellationToken cancellationToken) =>
        Task.WhenAll(
            DeleteAsync(containers.Value.Audio, NormalizedAudio.AudioBlobName(entraObjectId, recordUid), cancellationToken),
            DeleteAsync(containers.Value.Audio, NormalizedAudio.ErrorBlobName(entraObjectId, recordUid), cancellationToken),
            DeleteTranscriptAsync(entraObjectId, recordUid, cancellationToken));

    /// <summary>
    /// Elimina trascrizione ed esito di errore del job di trascrizione, che altrimenti salterebbe il record:
    /// serve per trascrivere di nuovo la copia normalizzata.
    /// </summary>
    public Task DeleteTranscriptAsync(Guid entraObjectId, Guid recordUid, CancellationToken cancellationToken) =>
        Task.WhenAll(
            DeleteAsync(containers.Value.Transcripts, Transcript.TextBlobName(entraObjectId, recordUid), cancellationToken),
            DeleteAsync(containers.Value.Transcripts, Transcript.ErrorBlobName(entraObjectId, recordUid), cancellationToken));

    /// <summary>
    /// Elimina tutti i blob del record, originale compreso (eliminazione del record da parte dell'utente).
    /// Restano recuperabili per 14 giorni grazie al soft delete dello storage.
    /// </summary>
    public async Task DeleteAllAsync(Guid entraObjectId, Guid recordUid, CancellationToken cancellationToken)
    {
        await DeleteDerivedAsync(entraObjectId, recordUid, cancellationToken);
        await Task.WhenAll(
            DeleteAsync(containers.Value.Uploads, UploadBlobName(entraObjectId, recordUid), cancellationToken),
            DeleteAsync(containers.Value.Documents, UploadBlobName(entraObjectId, recordUid), cancellationToken));
    }

    /// <summary>
    /// Elimina tutti i blob dell'utente nei quattro contenitori, cioè tutto ciò che sta sotto "&lt;oid&gt;/"
    /// (eliminazione dell'account). Comprende i file di record già eliminati dal database ma rimasti, ad
    /// esempio scritti dalla pipeline dopo l'eliminazione. Restano recuperabili per 14 giorni grazie al soft
    /// delete dello storage.
    /// </summary>
    public Task DeleteUserAsync(Guid entraObjectId, CancellationToken cancellationToken) =>
        Task.WhenAll(
            DeletePrefixAsync(containers.Value.Uploads, entraObjectId, cancellationToken),
            DeletePrefixAsync(containers.Value.Audio, entraObjectId, cancellationToken),
            DeletePrefixAsync(containers.Value.Transcripts, entraObjectId, cancellationToken),
            DeletePrefixAsync(containers.Value.Documents, entraObjectId, cancellationToken));

    private async Task DeletePrefixAsync(string container, Guid entraObjectId, CancellationToken cancellationToken)
    {
        var client = Container(container);
        var options = new GetBlobsOptions { Prefix = $"{entraObjectId:D}/" };
        await foreach (var blob in client.GetBlobsAsync(options, cancellationToken))
        {
            await client.GetBlobClient(blob.Name).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }

    private Task DeleteAsync(string container, string blobName, CancellationToken cancellationToken) =>
        Container(container).GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    private BlobContainerClient Container(string name) => blobServiceClient.GetBlobContainerClient(name);
}
