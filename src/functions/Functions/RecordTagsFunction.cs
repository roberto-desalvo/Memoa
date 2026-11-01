using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Tag di un record scelti dall'utente dalla pagina del record. La richiesta contiene tutti i tag che il
/// record deve mostrare: quelli aggiunti diventano manuali (anche se erano automatici o esclusi), quelli tolti
/// diventano esclusi, così l'assegnazione automatica non li riassegna. I tag nuovi si creano prima con
/// <see cref="TagFunctions"/>.
/// </summary>
public sealed class RecordTagsFunction(MemoaDbContext db, ILogger<RecordTagsFunction> logger)
{
    /// <summary>Limite di sicurezza sulla richiesta, ben oltre l'uso previsto.</summary>
    private const int MaxTags = 100;

    [Function("SetRecordTags")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "records/{recordUid:guid}/tags")] HttpRequest req,
        Guid recordUid,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadJsonOrDefaultAsync<SetRecordTagsRequest>(cancellationToken);
        if (body?.TagUids is null || body.TagUids.Count > MaxTags)
        {
            return new BadRequestObjectResult(new { error = "invalid_tags", maxCount = MaxTags });
        }

        var wanted = body.TagUids.ToHashSet();

        // Il filtro globale restituisce solo record e tag dell'utente autenticato. Si caricano tutte le righe,
        // anche le esclusioni, perché un tag escluso può tornare manuale.
        var record = await db.Records
            .Include(r => r.Attachments)
            .Include(r => r.Tags)
            .ThenInclude(rt => rt.Tag)
            .SingleOrDefaultAsync(r => r.RecordUid == recordUid, cancellationToken);

        if (record is null)
        {
            return new NotFoundResult();
        }

        var tags = await db.Tags
            .Where(t => wanted.Contains(t.TagUid))
            .ToListAsync(cancellationToken);

        if (tags.Count != wanted.Count)
        {
            // Tag eliminato nel frattempo o di un altro utente.
            return new BadRequestObjectResult(new { error = "unknown_tag" });
        }

        foreach (var tag in tags)
        {
            var row = record.Tags.FirstOrDefault(rt => rt.TagId == tag.Id);
            if (row is null)
            {
                record.Tags.Add(new RecordTag { TagId = tag.Id, Tag = tag, Source = RecordTagSource.Manual });
            }
            else if (row.Source != RecordTagSource.Manual)
            {
                row.Source = RecordTagSource.Manual;
                row.Similarity = null;
            }
        }

        foreach (var row in record.Tags.Where(rt => rt.Source != RecordTagSource.Excluded && !wanted.Contains(rt.Tag.TagUid)))
        {
            row.Source = RecordTagSource.Excluded;
            row.Similarity = null;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Assegnazione automatica concorrente (riga eliminata o inserita nel frattempo): il client ricarica
            // e riprova.
            logger.LogInformation("Tag del record {RecordUid} in conflitto con un aggiornamento concorrente", recordUid);
            return new ConflictObjectResult(new { error = "conflict" });
        }

        logger.LogInformation("Tag del record {RecordUid} aggiornati ({TagCount} visibili)", recordUid, wanted.Count);
        return new OkObjectResult(RecordResponses.ToDetail(record));
    }
}

public sealed record SetRecordTagsRequest(List<Guid>? TagUids);
