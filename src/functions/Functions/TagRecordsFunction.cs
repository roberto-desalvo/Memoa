using Azure.Storage.Queues.Models;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Embeddings;
using Memoa.Functions.Tagging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Functions;

/// <summary>
/// Assegnazione automatica dei tag, attivata dalla coda dei tag (<see cref="TaggingRequest"/>): un record
/// riceve un tag attivo se almeno uno dei suoi frammenti ha con il tag una similarità coseno non inferiore a
/// <see cref="TaggingOptions.MinSimilarity"/>. Si confrontano solo vettori dello stesso modello, già salvati
/// nel database: nessuna chiamata ad Azure OpenAI.
/// Si ricalcolano solo le assegnazioni automatiche: quelle manuali e le esclusioni scelte dall'utente non
/// cambiano, e un tag escluso non viene riassegnato. Idempotente: le assegnazioni automatiche coinvolte
/// vengono sostituite in una transazione.
/// </summary>
public sealed class TagRecordsFunction(
    MemoaDbContext db,
    IOptions<TaggingOptions> options,
    ILogger<TagRecordsFunction> logger)
{
    private const string CosineMetric = "cosine";

    [Function("TagRecords")]
    public async Task Run(
        [QueueTrigger("%Queues:Tagging%", Connection = "DataStorage")] QueueMessage message,
        CancellationToken cancellationToken)
    {
        // Solo le Functions scrivono su questa coda: un messaggio non valido non diventa valido riprovando.
        if (!TaggingRequest.TryParse(message.MessageText, out var request))
        {
            logger.LogError("Messaggio {MessageId} non valido sulla coda dei tag: scartato", message.MessageId);
            return;
        }

        if (request.RecordUid is { } recordUid)
        {
            await TagRecordAsync(recordUid, cancellationToken);
        }
        else
        {
            await ApplyTagAsync(request.TagUid!.Value, cancellationToken);
        }
    }

    /// <summary>Record appena indicizzato: confronto con tutti i tag attivi dell'utente.</summary>
    private async Task TagRecordAsync(Guid recordUid, CancellationToken cancellationToken)
    {
        var record = await db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Where(r => r.RecordUid == recordUid)
            .Select(r => new { r.Id, r.UserId, r.Status })
            .SingleOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            logger.LogWarning("Nessun record {RecordUid}: messaggio ignorato", recordUid);
            return;
        }

        // Testo modificato nel frattempo: la nuova indicizzazione accoderà di nuovo il record.
        if (record.Status != RecordStatus.Ready)
        {
            logger.LogInformation("Record {RecordUid} non pronto: messaggio ignorato", recordUid);
            return;
        }

        var chunks = db.RecordChunks
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Where(c => c.RecordId == record.Id);

        var scores = await db.Tags
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Where(t => t.UserId == record.UserId && t.IsActive)
            .Select(t => new
            {
                t.Id,
                // Null se il record non ha chunk dello stesso modello del tag.
                Distance = chunks
                    .Where(c => c.EmbeddingModel == t.EmbeddingModel)
                    .Min(c => (double?)EF.Functions.VectorDistance(CosineMetric, c.Embedding, t.Embedding)),
            })
            .ToListAsync(cancellationToken);

        var maxDistance = 1 - options.Value.MinSimilarity;
        var activeTagIds = scores.Select(s => s.Id).ToList();
        var matches = scores
            .Where(s => s.Distance <= maxDistance)
            .Select(s => (RecordId: record.Id, TagId: s.Id, Distance: s.Distance!.Value))
            .ToList();

        // Le assegnazioni automatiche dei tag disattivati restano com'erano.
        var assigned = await ReplaceAutoAsync(
            db.RecordTags.Where(rt => rt.RecordId == record.Id && activeTagIds.Contains(rt.TagId)),
            matches,
            cancellationToken);

        logger.LogInformation("Record {RecordUid}: {TagCount} tag assegnati in automatico", recordUid, assigned);
    }

    /// <summary>Tag creato, modificato o riattivato: confronto con tutti i record pronti dell'utente.</summary>
    private async Task ApplyTagAsync(Guid tagUid, CancellationToken cancellationToken)
    {
        var tag = await db.Tags
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Where(t => t.TagUid == tagUid)
            .Select(t => new { t.Id, t.UserId, t.IsActive, t.Embedding, t.EmbeddingModel })
            .SingleOrDefaultAsync(cancellationToken);

        if (tag is null)
        {
            logger.LogWarning("Nessun tag {TagUid}: messaggio ignorato", tagUid);
            return;
        }

        // Disattivato dopo l'accodamento: riattivandolo verrà accodato di nuovo.
        if (!tag.IsActive)
        {
            logger.LogInformation("Tag {TagUid} disattivato: messaggio ignorato", tagUid);
            return;
        }

        var maxDistance = 1 - options.Value.MinSimilarity;
        var embedding = tag.Embedding;
        var rows = await db.RecordChunks
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Where(c => c.Record.UserId == tag.UserId
                && c.Record.Status == RecordStatus.Ready
                && c.EmbeddingModel == tag.EmbeddingModel)
            .GroupBy(c => c.RecordId)
            .Select(g => new
            {
                RecordId = g.Key,
                Distance = g.Min(c => EF.Functions.VectorDistance(CosineMetric, c.Embedding, embedding)),
            })
            .Where(r => r.Distance <= maxDistance)
            .ToListAsync(cancellationToken);

        var matches = rows.Select(r => (r.RecordId, TagId: tag.Id, r.Distance)).ToList();

        // Anche le assegnazioni a record non più pronti: il tag è cambiato, la rielaborazione le ricalcolerà.
        var assigned = await ReplaceAutoAsync(
            db.RecordTags.Where(rt => rt.TagId == tag.Id),
            matches,
            cancellationToken);

        logger.LogInformation("Tag {TagUid}: assegnato in automatico a {RecordCount} record", tagUid, assigned);
    }

    /// <summary>
    /// In una transazione elimina le assegnazioni automatiche nell'ambito indicato e inserisce quelle nuove,
    /// tranne dove l'utente ha già scelto (assegnazione manuale o esclusione). Restituisce quante ne inserisce.
    /// </summary>
    private async Task<int> ReplaceAutoAsync(
        IQueryable<RecordTag> scope,
        IReadOnlyList<(int RecordId, int TagId, double Distance)> matches,
        CancellationToken cancellationToken)
    {
        scope = scope.IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter]);

        // Con i retry attivi la transazione esplicita va eseguita nella strategia di esecuzione.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            // Un nuovo tentativo riparte da zero, senza le entità aggiunte da quello fallito.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            await scope.Where(rt => rt.Source == RecordTagSource.Auto).ExecuteDeleteAsync(ct);

            var chosen = (await scope
                    .Select(rt => new { rt.RecordId, rt.TagId })
                    .ToListAsync(ct))
                .Select(rt => (rt.RecordId, rt.TagId))
                .ToHashSet();

            var added = matches
                .Where(m => !chosen.Contains((m.RecordId, m.TagId)))
                .Select(m => new RecordTag
                {
                    RecordId = m.RecordId,
                    TagId = m.TagId,
                    Source = RecordTagSource.Auto,
                    Similarity = Math.Round(1 - m.Distance, 3),
                })
                .ToList();

            db.RecordTags.AddRange(added);

            // Un'esecuzione concorrente che ha già inserito la stessa coppia viola la chiave primaria: la
            // coda riprova sui dati aggiornati.
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return added.Count;
        }, cancellationToken);
    }
}
