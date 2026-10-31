using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Chiamata dal client quando l'upload su Blob Storage fallisce: elimina il record creato da
/// <see cref="CreateAudioUploadFunction"/>, che altrimenti resterebbe "in elaborazione" senza file.
/// Elimina solo un record il cui file non è mai arrivato: se l'upload risulta completato (blob presente o
/// già collegato dalla validazione) risponde 409 e il record prosegue nella pipeline.
/// Idempotente: un record già eliminato risponde 204.
/// </summary>
public sealed class CancelAudioUploadFunction(
    MemoaDbContext db,
    CurrentUser currentUser,
    RecordBlobService blobs,
    ILogger<CancelAudioUploadFunction> logger)
{
    [Function("CancelAudioUpload")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "uploads/audio/{recordUid:guid}")] HttpRequest req,
        Guid recordUid,
        CancellationToken cancellationToken)
    {
        // Il filtro globale restituisce solo i record dell'utente autenticato.
        var record = await db.Records
            .Include(r => r.Attachments)
            .SingleOrDefaultAsync(r => r.RecordUid == recordUid, cancellationToken);

        if (record is null)
        {
            return new NoContentResult();
        }

        var uploadCompleted = record.SourceType != SourceType.Audio
            || record.Status != RecordStatus.Processing
            || record.Attachments.Count > 0
            // Il blob esiste ma l'evento non è ancora arrivato alla validazione (es. risposta persa dopo
            // un upload riuscito).
            || await blobs.UploadExistsAsync(currentUser.RequiredEntraObjectId, recordUid, cancellationToken);

        if (uploadCompleted)
        {
            return new ConflictObjectResult(new { error = "upload_completed" });
        }

        db.Records.Remove(record);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // La validazione ha collegato il file nel frattempo (RowVersion): l'upload è riuscito.
            return new ConflictObjectResult(new { error = "upload_completed" });
        }

        logger.LogInformation("Upload non riuscito: record {RecordUid} eliminato", recordUid);
        return new NoContentResult();
    }
}
