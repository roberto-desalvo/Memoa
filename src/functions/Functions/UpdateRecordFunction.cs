using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Modifica dalla pagina del record: titolo, data della nota e testo (anche la correzione della
/// trascrizione, che resta invariata in OriginalTranscript). Un testo modificato riporta il record "in
/// elaborazione" e va indicizzato di nuovo.
/// </summary>
public sealed class UpdateRecordFunction(MemoaDbContext db, ILogger<UpdateRecordFunction> logger)
{
    [Function("UpdateRecord")]
    public async Task<HttpWithIndexingOutput> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "records/{recordUid:guid}")] HttpRequest req,
        Guid recordUid,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadJsonOrDefaultAsync<UpdateRecordRequest>(cancellationToken);
        if (body is null)
        {
            return new HttpWithIndexingOutput { Result = new BadRequestResult() };
        }

        // Il filtro globale restituisce solo i record dell'utente autenticato.
        var record = await db.Records
            .Include(r => r.Attachments)
            .IncludeTags()
            .SingleOrDefaultAsync(r => r.RecordUid == recordUid, cancellationToken);

        if (record is null)
        {
            return new HttpWithIndexingOutput { Result = new NotFoundResult() };
        }

        if (body.Title is not null)
        {
            if (!RecordInput.TryNormalizeTitle(body.Title, out var title))
            {
                return new HttpWithIndexingOutput { Result = RecordInput.InvalidTitle() };
            }

            record.Title = title;
        }

        if (body.OccurredAt is { } occurredAt)
        {
            record.OccurredAt = occurredAt.UtcDateTime;
        }

        var reindex = false;
        if (body.Text is not null && body.Text != record.Text)
        {
            if (!RecordInput.IsValidText(body.Text))
            {
                return new HttpWithIndexingOutput { Result = RecordInput.InvalidText() };
            }

            // Senza testo (audio non ancora trascritto), durante l'elaborazione o dopo una rigenerazione
            // della trascrizione non riuscita (OriginalTranscript null) il testo non si modifica:
            // l'indicizzazione in corso lo sovrascriverebbe o ne verrebbe sovrascritta.
            if (record.Text is null
                || record.Status == RecordStatus.Processing
                || (record.SourceType == SourceType.Audio && record.OriginalTranscript is null))
            {
                return new HttpWithIndexingOutput
                {
                    Result = new ConflictObjectResult(new { error = "text_not_editable" }),
                };
            }

            record.Text = body.Text;
            record.Status = RecordStatus.Processing;
            record.ErrorCode = null;
            reindex = true;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // La pipeline ha modificato il record nel frattempo: il client ricarica e riprova.
            logger.LogInformation("Modifica del record {RecordUid} in conflitto con un aggiornamento concorrente", recordUid);
            return new HttpWithIndexingOutput { Result = new ConflictObjectResult(new { error = "conflict" }) };
        }

        if (reindex)
        {
            logger.LogInformation("Testo del record {RecordUid} modificato: accodato per l'indicizzazione", recordUid);
        }

        return new HttpWithIndexingOutput
        {
            Result = new OkObjectResult(RecordResponses.ToDetail(record)),
            IndexingMessage = reindex ? record.RecordUid.ToString("D") : null,
        };
    }
}

/// <summary>Campi da modificare; quelli null restano invariati.</summary>
public sealed record UpdateRecordRequest(string? Title, DateTimeOffset? OccurredAt, string? Text);
