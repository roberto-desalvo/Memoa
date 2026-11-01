using Azure.Storage.Blobs;
using Memoa.Shared.Pipeline;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Storage;

/// <summary>Blob di un record nei tre contenitori, tutti sotto il prefisso "&lt;oid&gt;/".</summary>
public sealed class RecordBlobService(BlobServiceClient blobServiceClient, IOptions<ContainerOptions> containers)
{
    /// <summary>
    /// Percorso dell'originale nel contenitore degli originali. Prefisso per utente: la cancellazione GDPR
    /// elimina tutti i suoi file con un solo prefisso. Nessuna estensione: il formato reale viene rilevato
    /// con ffmpeg durante la normalizzazione.
    /// </summary>
    public static string UploadBlobName(Guid entraObjectId, Guid recordUid) => $"{entraObjectId:D}/{recordUid:D}";

    /// <summary>URL dell'originale, senza SAS: è il messaggio della coda di normalizzazione.</summary>
    public string UploadBlobUrl(string originalBlobPath) =>
        Container(containers.Value.Uploads).GetBlobClient(originalBlobPath).Uri.ToString();

    /// <summary>URL della copia normalizzata, senza SAS: va nel messaggio della coda di trascrizione.</summary>
    public string NormalizedBlobUrl(string normalizedBlobPath) =>
        Container(containers.Value.Audio).GetBlobClient(normalizedBlobPath).Uri.ToString();

    /// <summary>True se il client ha completato l'upload dell'originale.</summary>
    public async Task<bool> UploadExistsAsync(Guid entraObjectId, Guid recordUid, CancellationToken cancellationToken) =>
        await Container(containers.Value.Uploads)
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
        await DeleteAsync(containers.Value.Uploads, UploadBlobName(entraObjectId, recordUid), cancellationToken);
    }

    private Task DeleteAsync(string container, string blobName, CancellationToken cancellationToken) =>
        Container(container).GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    private BlobContainerClient Container(string name) => blobServiceClient.GetBlobContainerClient(name);
}
