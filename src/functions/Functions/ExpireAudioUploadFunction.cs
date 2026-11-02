using Azure.Storage.Queues.Models;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Attivata dal messaggio ritardato scritto alla richiesta del SAS (<see cref="UploadExpiryScheduler"/>),
/// quando il SAS è già scaduto: se il file non è mai arrivato elimina il record, che altrimenti resterebbe
/// "in elaborazione" per sempre (e il frontend continuerebbe il polling). Vale per gli upload audio e per
/// quelli dei file di testo.
/// Idempotente: un record già eliminato o con il file arrivato viene ignorato.
/// </summary>
public sealed class ExpireAudioUploadFunction(
    MemoaDbContext db,
    RecordBlobService blobs,
    ILogger<ExpireAudioUploadFunction> logger)
{
    [Function("ExpireAudioUpload")]
    public async Task Run(
        [QueueTrigger("%Queues:UploadExpiry%", Connection = "DataStorage")] QueueMessage message,
        CancellationToken cancellationToken)
    {
        // Solo CreateAudioUpload e CreateDocumentUpload scrivono su questa coda: un messaggio non valido non
        // diventa valido riprovando.
        if (!Guid.TryParseExact(message.MessageText, "D", out var recordUid))
        {
            logger.LogError("Messaggio {MessageId} non valido sulla coda di scadenza degli upload: scartato", message.MessageId);
            return;
        }

        var record = await db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Include(r => r.User)
            .Include(r => r.Attachments)
            .SingleOrDefaultAsync(r => r.RecordUid == recordUid, cancellationToken);

        // Eliminato dall'utente o dal client dopo un upload fallito.
        if (record is null)
        {
            return;
        }

        // La validazione ha collegato il file: l'upload è arrivato. Una nota scritta non ha upload.
        if (record.Attachments.Count > 0 || record.Text is not null)
        {
            return;
        }

        // Il file c'è ma la validazione non l'ha ancora collegato: l'evento "blob creato" è in ritardo o è
        // andato perso (gestione degli eventi non consegnati da definire). Il record non va eliminato.
        if (await blobs.UploadExistsAsync(record.User.EntraObjectId, recordUid, record.SourceType, cancellationToken))
        {
            logger.LogWarning("Record {RecordUid}: originale presente ma non ancora validato", recordUid);
            return;
        }

        db.Records.Remove(record);

        // Se la validazione modifica il record nel frattempo (RowVersion) l'eccezione fa riprovare la coda,
        // che al tentativo successivo trova l'allegato.
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Upload del record {RecordUid} ({SourceType}) mai completato: record eliminato", recordUid, record.SourceType);
    }
}
