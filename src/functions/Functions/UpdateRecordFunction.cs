using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Embeddings;
using Memoa.Functions.Tagging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace Memoa.Functions.Functions;

/// <summary>
/// Modifica dalla pagina del record: titolo, data della nota e testo (anche la correzione della
/// trascrizione, che resta invariata in OriginalTranscript). Un testo modificato riporta il record "in
/// elaborazione" e va indicizzato di nuovo. Un titolo modificato su un record pronto ne ricalcola subito
/// l'embedding e riaccoda il record per i tag; negli altri stati l'embedding lo calcolerà l'indicizzazione.
/// </summary>
public sealed class UpdateRecordFunction(
    MemoaDbContext db,
    EmbeddingClient embeddingClient,
    ILogger<UpdateRecordFunction> logger)
{
    [Function("UpdateRecord")]
    public async Task<UpdateRecordOutput> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "records/{recordUid:guid}")] HttpRequest req,
        Guid recordUid,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadJsonOrDefaultAsync<UpdateRecordRequest>(cancellationToken);
        if (body is null)
        {
            return new UpdateRecordOutput { Result = new BadRequestResult() };
        }

        // Il filtro globale restituisce solo i record dell'utente autenticato.
        var record = await db.Records
            .Include(r => r.Attachments)
            .IncludeTags()
            .SingleOrDefaultAsync(r => r.RecordUid == recordUid, cancellationToken);

        if (record is null)
        {
            return new UpdateRecordOutput { Result = new NotFoundResult() };
        }

        var titleChanged = false;
        if (body.Title is not null)
        {
            if (!RecordInput.TryNormalizeTitle(body.Title, out var title))
            {
                return new UpdateRecordOutput { Result = RecordInput.InvalidTitle() };
            }

            titleChanged = title != record.Title;
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
                return new UpdateRecordOutput { Result = RecordInput.InvalidText() };
            }

            // Senza testo (audio non ancora trascritto), durante l'elaborazione o dopo una rigenerazione
            // della trascrizione non riuscita (OriginalTranscript null) il testo non si modifica:
            // l'indicizzazione in corso lo sovrascriverebbe o ne verrebbe sovrascritta.
            if (record.Text is null
                || record.Status == RecordStatus.Processing
                || (record.SourceType == SourceType.Audio && record.OriginalTranscript is null))
            {
                return new UpdateRecordOutput
                {
                    Result = new ConflictObjectResult(new { error = "text_not_editable" }),
                };
            }

            record.Text = body.Text;
            record.Status = RecordStatus.Processing;
            record.ErrorCode = null;
            record.TagsPendingSince = DateTime.UtcNow;
            reindex = true;
        }

        var retag = false;
        if (titleChanged)
        {
            if (record.Status == RecordStatus.Ready)
            {
                if (!await TrySetTitleEmbeddingAsync(record, cancellationToken))
                {
                    return new UpdateRecordOutput { Result = TagInput.EmbeddingUnavailable() };
                }

                record.TagsPendingSince = DateTime.UtcNow;
                retag = true;
            }
            else
            {
                // Il vettore del vecchio titolo non vale più. Ogni ripartenza (indicizzazione in corso, nuovo
                // tentativo) passa dall'indicizzazione, che lo ricalcola; un'indicizzazione già avviata con il
                // vecchio titolo fallisce sul RowVersion e riprova.
                record.TitleEmbedding = null;
                record.TitleEmbeddingModel = null;
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // La pipeline ha modificato il record nel frattempo: il client ricarica e riprova.
            logger.LogInformation("Modifica del record {RecordUid} in conflitto con un aggiornamento concorrente", recordUid);
            return new UpdateRecordOutput { Result = new ConflictObjectResult(new { error = "conflict" }) };
        }

        if (reindex)
        {
            logger.LogInformation("Testo del record {RecordUid} modificato: accodato per l'indicizzazione", recordUid);
        }
        else if (retag)
        {
            logger.LogInformation("Titolo del record {RecordUid} modificato: accodato per i tag", recordUid);
        }

        return new UpdateRecordOutput
        {
            Result = new OkObjectResult(RecordResponses.ToDetail(record)),
            IndexingMessage = reindex ? record.RecordUid.ToString("D") : null,
            TaggingMessage = retag ? TaggingRequest.ForRecord(record.RecordUid).Serialize() : null,
        };
    }

    /// <summary>False se Azure OpenAI non risponde: il client riceve un errore e nulla viene salvato.</summary>
    private async Task<bool> TrySetTitleEmbeddingAsync(Record record, CancellationToken cancellationToken)
    {
        try
        {
            var embeddings = await embeddingClient.GenerateCheckedEmbeddingsAsync([record.Title], cancellationToken);
            record.TitleEmbedding = new SqlVector<float>(embeddings[0].ToFloats());
            record.TitleEmbeddingModel = embeddings.Model;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Embedding del titolo del record {RecordUid} non calcolato", record.RecordUid);
            return false;
        }
    }
}

/// <summary>Campi da modificare; quelli null restano invariati.</summary>
public sealed record UpdateRecordRequest(string? Title, DateTimeOffset? OccurredAt, string? Text);
