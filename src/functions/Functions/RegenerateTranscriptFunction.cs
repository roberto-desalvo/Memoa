using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Storage;
using Memoa.Shared.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Rigenera la trascrizione automatica di un audio pronto (vedi
/// <see cref="RecordResponses.CanRegenerateTranscript"/>): elimina trascrizione ed esito di un'eventuale
/// trascrizione precedente del job di ripiego, che altrimenti salterebbe il record, azzera
/// OriginalTranscript, riporta il record "in elaborazione" e accoda di nuovo la copia normalizzata sulla
/// coda di trascrizione: come la prima volta si prova prima Azure AI Speech
/// (<see cref="TranscribeRecordFunction"/>), poi eventualmente il job.
/// Il testo corrente resta finché non arriva la nuova trascrizione, che lo sostituisce anche se modificato
/// dall'utente; <see cref="EnqueueIndexingFunction"/> rifà l'indicizzazione solo se il testo cambia.
/// </summary>
public sealed class RegenerateTranscriptFunction(
    MemoaDbContext db,
    CurrentUser currentUser,
    RecordBlobService blobs,
    ILogger<RegenerateTranscriptFunction> logger)
{
    [Function("RegenerateTranscript")]
    public async Task<HttpWithTranscriptionOutput> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "records/{recordUid:guid}/transcript/regenerate")] HttpRequest req,
        Guid recordUid,
        CancellationToken cancellationToken)
    {
        // Il filtro globale restituisce solo i record dell'utente autenticato.
        var record = await db.Records
            .Include(r => r.User)
            .Include(r => r.Attachments)
            .IncludeTags()
            .SingleOrDefaultAsync(r => r.RecordUid == recordUid, cancellationToken);

        if (record is null)
        {
            return new HttpWithTranscriptionOutput { Result = new NotFoundResult() };
        }

        var attachment = record.Attachments.FirstOrDefault(a => a.Kind == AttachmentKind.Audio);
        if (!RecordResponses.CanRegenerateTranscript(record.Status, record.SourceType, attachment?.NormalizedBlobPath))
        {
            return new HttpWithTranscriptionOutput { Result = new ConflictObjectResult(new { error = "not_regenerable" }) };
        }

        await blobs.DeleteTranscriptAsync(currentUser.RequiredEntraObjectId, recordUid, cancellationToken);

        record.OriginalTranscript = null;
        record.Status = RecordStatus.Processing;
        record.ErrorCode = null;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Il record resta com'era: senza il blob della trascrizione il testo è comunque nel database.
            return new HttpWithTranscriptionOutput { Result = new ConflictObjectResult(new { error = "conflict" }) };
        }

        logger.LogInformation("Record {RecordUid} accodato per rigenerare la trascrizione", recordUid);

        // La lingua dell'account serve come ripiego se il rilevamento automatico è incerto.
        return new HttpWithTranscriptionOutput
        {
            Result = new OkObjectResult(RecordResponses.ToDetail(record)),
            TranscriptionMessage = new TranscriptionRequest(
                blobs.NormalizedBlobUrl(attachment!.NormalizedBlobPath!),
                record.User.PreferredLanguage).Serialize(),
        };
    }
}
