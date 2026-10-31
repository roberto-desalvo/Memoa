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
/// "Riprova" su un record in errore (vedi <see cref="RecordResponses.CanRetry"/>):
/// <list type="bullet">
/// <item>record con testo (testo scritto, o audio la cui indicizzazione è fallita): si rifà solo
/// l'indicizzazione, senza perdere il testo e le eventuali correzioni dell'utente;</item>
/// <item>audio senza testo: si riparte dalla normalizzazione dell'originale, dopo aver eliminato copia
/// normalizzata, trascrizione ed esiti di errore, che altrimenti farebbero saltare il record ai job.</item>
/// </list>
/// </summary>
public sealed class RetryRecordFunction(
    MemoaDbContext db,
    CurrentUser currentUser,
    RecordBlobService blobs,
    ILogger<RetryRecordFunction> logger)
{
    [Function("RetryRecord")]
    public async Task<RetryOutput> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "records/{recordUid:guid}/retry")] HttpRequest req,
        Guid recordUid,
        CancellationToken cancellationToken)
    {
        // Il filtro globale restituisce solo i record dell'utente autenticato.
        var record = await db.Records
            .Include(r => r.Attachments)
            .SingleOrDefaultAsync(r => r.RecordUid == recordUid, cancellationToken);

        if (record is null)
        {
            return new RetryOutput { Result = new NotFoundResult() };
        }

        var attachment = record.Attachments.FirstOrDefault(a => a.Kind == AttachmentKind.Audio);
        if (!RecordResponses.CanRetry(record.Status, record.SourceType, record.ErrorCode, record.Text is not null, attachment is not null))
        {
            return new RetryOutput { Result = new ConflictObjectResult(new { error = "not_retryable" }) };
        }

        var previousError = record.ErrorCode;
        var reindex = record.Text is not null;

        if (!reindex)
        {
            await blobs.DeleteDerivedAsync(currentUser.RequiredEntraObjectId, recordUid, cancellationToken);
            attachment!.NormalizedBlobPath = null;
            attachment.DurationSeconds = null;
        }

        record.Status = RecordStatus.Processing;
        record.ErrorCode = null;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new RetryOutput { Result = new ConflictObjectResult(new { error = "conflict" }) };
        }

        logger.LogInformation(
            "Record {RecordUid} riprovato dopo l'errore {ErrorCode}: {Step}",
            recordUid, previousError, reindex ? "indicizzazione" : "normalizzazione");

        return new RetryOutput
        {
            Result = new OkObjectResult(RecordResponses.ToDetail(record)),
            IndexingMessage = reindex ? record.RecordUid.ToString("D") : null,
            NormalizationMessage = reindex ? null : blobs.UploadBlobUrl(attachment!.OriginalBlobPath),
        };
    }
}
