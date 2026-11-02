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
/// <see cref="CreateAudioUploadFunction"/> o da <see cref="CreateDocumentUploadFunction"/>, che altrimenti
/// resterebbe "in elaborazione" senza file.
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
    /// <param name="kind">"audio" (<see cref="CreateAudioUploadFunction"/>) o "document" (<see cref="CreateDocumentUploadFunction"/>).</param>
    [Function("CancelAudioUpload")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "uploads/{kind}/{recordUid:guid}")] HttpRequest req,
        string kind,
        Guid recordUid,
        CancellationToken cancellationToken)
    {
        SourceType? sourceType = kind switch
        {
            "audio" => SourceType.Audio,
            "document" => SourceType.Text,
            _ => null,
        };

        if (sourceType is null)
        {
            return new NotFoundResult();
        }

        // Il filtro globale restituisce solo i record dell'utente autenticato.
        var record = await db.Records
            .Include(r => r.Attachments)
            .SingleOrDefaultAsync(r => r.RecordUid == recordUid, cancellationToken);

        if (record is null)
        {
            return new NoContentResult();
        }

        var uploadCompleted = record.SourceType != sourceType
            || record.Status != RecordStatus.Processing
            || record.Attachments.Count > 0
            // Nota scritta: il testo arriva con la richiesta, non c'è alcun upload.
            || record.Text is not null
            // Il blob esiste ma l'evento non è ancora arrivato alla validazione (es. risposta persa dopo
            // un upload riuscito).
            || await blobs.UploadExistsAsync(currentUser.RequiredEntraObjectId, recordUid, record.SourceType, cancellationToken);

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

        logger.LogInformation("Upload non riuscito: record {RecordUid} ({SourceType}) eliminato", recordUid, record.SourceType);
        return new NoContentResult();
    }
}
