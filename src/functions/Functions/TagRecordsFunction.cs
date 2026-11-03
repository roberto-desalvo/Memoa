using System.Linq.Expressions;
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
/// riceve un tag attivo se almeno uno dei suoi frammenti ha con il tag (nome e descrizione) una similarità
/// coseno non inferiore a <see cref="TaggingOptions.MinSimilarity"/>. Si confrontano solo vettori dello stesso
/// modello, già salvati nel database: nessuna chiamata ad Azure OpenAI.
/// Si ricalcolano solo le assegnazioni automatiche: quelle manuali e le esclusioni scelte dall'utente non
/// cambiano, e un tag escluso non viene riassegnato. Idempotente: le assegnazioni automatiche coinvolte
/// vengono sostituite in una transazione, insieme alla fine dell'attesa del record o del tag
/// (<see cref="TagStatus"/>).
/// </summary>
public sealed class TagRecordsFunction(
    MemoaDbContext db,
    IOptions<TaggingOptions> options,
    ILogger<TagRecordsFunction> logger)
{
    private const string CosineMetric = "cosine";

    /// <summary>Uguale a extensions.queues.maxDequeueCount in host.json.</summary>
    private const int MaxDequeueCount = 5;

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

        try
        {
            if (request.RecordUid is { } recordUid)
            {
                await TagRecordAsync(recordUid, cancellationToken);
            }
            else
            {
                await ApplyTagAsync(request.TagUid!.Value, cancellationToken);
            }
        }
        catch (Exception ex) when (message.DequeueCount >= MaxDequeueCount && ex is not OperationCanceledException)
        {
            // Ultimo tentativo: senza questo i tag resterebbero "in assegnazione" fino al timeout. Il
            // messaggio va comunque nella coda dei messaggi non elaborabili.
            logger.LogError(ex, "Assegnazione dei tag non riuscita dopo {DequeueCount} tentativi", message.DequeueCount);
            await ClearPendingAsync(request, cancellationToken);
            throw;
        }
    }

    /// <summary>Record appena indicizzato: confronto con tutti i tag attivi dell'utente.</summary>
    private async Task TagRecordAsync(Guid recordUid, CancellationToken cancellationToken)
    {
        var record = await db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Where(r => r.RecordUid == recordUid)
            .Select(r => new { r.Id, r.UserId, r.Status, r.TagsPendingSince })
            .SingleOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            logger.LogWarning("Nessun record {RecordUid}: messaggio ignorato", recordUid);
            return;
        }

        // Testo modificato nel frattempo: la nuova indicizzazione accoderà di nuovo il record, e l'attesa
        // resta finché non arriva.
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
                // Minimo con TOP(1) e non con Min: SQL Server non accetta un'aggregazione che combina colonne
                // della sottoquery con quelle esterne del tag (errore 8124).
                // Null se il record non ha chunk dello stesso modello del tag.
                ContentDistance = chunks
                    .Where(c => c.EmbeddingModel == t.EmbeddingModel)
                    .Select(c => (double?)EF.Functions.VectorDistance(CosineMetric, c.Embedding, t.Embedding))
                    .OrderBy(d => d)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var maxDistance = 1 - options.Value.MinSimilarity;
        var activeTagIds = scores.Select(s => s.Id).ToList();
        var matches = scores
            .Where(s => s.ContentDistance <= maxDistance)
            .Select(s => (RecordId: record.Id, TagId: s.Id, Distance: s.ContentDistance!.Value))
            .ToList();

        // Le assegnazioni automatiche dei tag disattivati restano com'erano. L'attesa si chiude solo se è
        // ancora quella letta: una modifica successiva del record ne ha aperta una nuova.
        var assigned = await ReplaceAutoAsync(
            db.RecordTags.Where(rt => rt.RecordId == record.Id && activeTagIds.Contains(rt.TagId)),
            matches,
            ct => EndRecordPendingAsync(r => r.Id == record.Id && r.TagsPendingSince == record.TagsPendingSince, ct),
            cancellationToken);

        logger.LogInformation("Record {RecordUid}: {TagCount} tag assegnati in automatico", recordUid, assigned);
    }

    /// <summary>Tag creato, modificato o riattivato: confronto con tutti i record pronti dell'utente.</summary>
    private async Task ApplyTagAsync(Guid tagUid, CancellationToken cancellationToken)
    {
        var tag = await db.Tags
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Where(t => t.TagUid == tagUid)
            .Select(t => new { t.Id, t.UserId, t.IsActive, t.Embedding, t.EmbeddingModel, t.AssignmentPendingSince })
            .SingleOrDefaultAsync(cancellationToken);

        if (tag is null)
        {
            logger.LogWarning("Nessun tag {TagUid}: messaggio ignorato", tagUid);
            return;
        }

        // L'attesa si chiude solo se è ancora quella letta: una modifica successiva del tag ne ha aperta una
        // nuova, con il proprio messaggio.
        Func<CancellationToken, Task> endPending = ct =>
            EndTagPendingAsync(t => t.Id == tag.Id && t.AssignmentPendingSince == tag.AssignmentPendingSince, ct);

        // Disattivato dopo l'accodamento: riattivandolo verrà accodato di nuovo.
        if (!tag.IsActive)
        {
            logger.LogInformation("Tag {TagUid} disattivato: messaggio ignorato", tagUid);
            await endPending(cancellationToken);
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
            endPending,
            cancellationToken);

        logger.LogInformation("Tag {TagUid}: assegnato in automatico a {RecordCount} record", tagUid, assigned);
    }

    /// <summary>
    /// In una transazione elimina le assegnazioni automatiche nell'ambito indicato, inserisce quelle nuove
    /// (tranne dove l'utente ha già scelto: assegnazione manuale o esclusione) e chiude l'attesa con
    /// <paramref name="endPending"/>. Restituisce quante assegnazioni inserisce.
    /// </summary>
    private async Task<int> ReplaceAutoAsync(
        IQueryable<RecordTag> scope,
        IReadOnlyList<(int RecordId, int TagId, double Distance)> matches,
        Func<CancellationToken, Task> endPending,
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
            await endPending(ct);
            await transaction.CommitAsync(ct);
            return added.Count;
        }, cancellationToken);
    }

    /// <summary>
    /// Tentativi esauriti: chiude comunque l'attesa del record o del tag, così lo stato torna quello finale.
    /// Aggiornamento diretto, indipendente dalle entità tracciate dal tentativo fallito.
    /// </summary>
    private async Task ClearPendingAsync(TaggingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.RecordUid is { } recordUid)
            {
                await EndRecordPendingAsync(r => r.RecordUid == recordUid, cancellationToken);
            }
            else
            {
                await EndTagPendingAsync(t => t.TagUid == request.TagUid, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Es. database non raggiungibile: resta il timeout. Si propaga l'errore originale.
            logger.LogError(ex, "Impossibile chiudere l'attesa dell'assegnazione dei tag");
        }
    }

    private Task EndRecordPendingAsync(Expression<Func<Record, bool>> predicate, CancellationToken cancellationToken) =>
        db.Records
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Where(predicate)
            .Where(r => r.TagsPendingSince != null)
            // Non è una modifica dell'utente: UpdatedAt resta quello (ExecuteUpdate non passa da SaveChanges).
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.TagsPendingSince, (DateTime?)null), cancellationToken);

    private Task EndTagPendingAsync(Expression<Func<Tag, bool>> predicate, CancellationToken cancellationToken) =>
        db.Tags
            .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
            .Where(predicate)
            .Where(t => t.AssignmentPendingSince != null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.AssignmentPendingSince, (DateTime?)null), cancellationToken);
}
