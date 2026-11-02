using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Storage;

/// <summary>
/// Pulizia dei record (audio o file di testo) il cui upload non arriva mai (app chiusa durante l'upload, eliminazione dal
/// client non riuscita). Alla richiesta del SAS si scrive sulla coda un messaggio con il solo RecordUid,
/// invisibile fino alla scadenza del SAS più un margine: allora l'upload non può più avvenire e
/// <see cref="Functions.ExpireAudioUploadFunction"/> elimina il record se il file non c'è. Un messaggio per
/// upload, nessun timer che interroga il database.
/// </summary>
public sealed class UploadExpiryScheduler(QueueServiceClient queueServiceClient, IOptions<QueueOptions> queues)
{
    /// <summary>
    /// Il SAS non permette di creare il blob dopo la scadenza; il margine copre orologi non allineati e la
    /// consegna dell'evento "blob creato" alla validazione per un upload completato all'ultimo momento.
    /// </summary>
    public static readonly TimeSpan Delay = BlobSasService.UploadSasLifetime + TimeSpan.FromMinutes(15);

    public async Task ScheduleAsync(Guid recordUid, CancellationToken cancellationToken)
    {
        var queue = queueServiceClient.GetQueueClient(queues.Value.UploadExpiry);
        await queue.SendMessageAsync(recordUid.ToString("D"), visibilityTimeout: Delay, cancellationToken: cancellationToken);
    }
}
