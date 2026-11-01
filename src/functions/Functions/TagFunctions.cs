using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Tagging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace Memoa.Functions.Functions;

/// <summary>
/// Gestione dei tag dell'utente. L'embedding di nome e descrizione si calcola subito, alla creazione e a
/// ogni modifica di nome o descrizione; poi il tag va sulla coda dei tag (<see cref="TagRecordsFunction"/>),
/// che lo applica ai record già esistenti. Nomi e descrizioni non finiscono mai nei log.
/// </summary>
public sealed class TagFunctions(
    MemoaDbContext db,
    CurrentUser currentUser,
    EmbeddingClient embeddingClient,
    ILogger<TagFunctions> logger)
{
    [Function("ListTags")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tags")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        // Il filtro globale limita la query ai tag dell'utente autenticato.
        var tags = await db.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TagSummary(
                t.TagUid,
                t.Name,
                t.Description,
                t.IsActive,
                t.RecordTags.Count(rt => rt.Source != RecordTagSource.Excluded)))
            .ToListAsync(cancellationToken);

        return new OkObjectResult(tags);
    }

    [Function("GetTag")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tags/{tagUid:guid}")] HttpRequest req,
        Guid tagUid,
        CancellationToken cancellationToken)
    {
        var tag = await db.Tags
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TagUid == tagUid, cancellationToken);

        if (tag is null)
        {
            return new NotFoundResult();
        }

        var records = await db.Records
            .Where(r => r.Tags.Any(rt => rt.TagId == tag.Id && rt.Source != RecordTagSource.Excluded))
            .ToSummariesAsync(cancellationToken);

        return new OkObjectResult(new TagDetail(tag.TagUid, tag.Name, tag.Description, tag.IsActive, records));
    }

    [Function("CreateTag")]
    public async Task<HttpWithTaggingOutput> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tags")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadJsonOrDefaultAsync<CreateTagRequest>(cancellationToken);
        if (body is null || !TagInput.TryNormalizeName(body.Name, out var name))
        {
            return new HttpWithTaggingOutput { Result = TagInput.InvalidName() };
        }

        if (!TagInput.TryNormalizeDescription(body.Description, out var description))
        {
            return new HttpWithTaggingOutput { Result = TagInput.InvalidDescription() };
        }

        if (await db.Tags.AnyAsync(t => t.Name == name, cancellationToken))
        {
            return new HttpWithTaggingOutput { Result = TagInput.NameTaken() };
        }

        var entraObjectId = currentUser.RequiredEntraObjectId;

        // Creato da UserProvisioningMiddleware prima di questa funzione.
        var userId = await db.Users
            .Where(u => u.EntraObjectId == entraObjectId)
            .Select(u => u.Id)
            .SingleAsync(cancellationToken);

        var tag = new Tag
        {
            TagUid = Guid.CreateVersion7(),
            UserId = userId,
            Name = name,
            Description = description,
        };

        if (!await TrySetEmbeddingAsync(tag, cancellationToken))
        {
            return new HttpWithTaggingOutput { Result = TagInput.EmbeddingUnavailable() };
        }

        db.Tags.Add(tag);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Stesso nome creato nel frattempo (indice univoco); ogni altro errore si propaga.
            db.Entry(tag).State = EntityState.Detached;
            if (await db.Tags.AnyAsync(t => t.Name == name, cancellationToken))
            {
                return new HttpWithTaggingOutput { Result = TagInput.NameTaken() };
            }

            throw;
        }

        logger.LogInformation("Tag {TagUid} creato e accodato per l'assegnazione", tag.TagUid);

        return new HttpWithTaggingOutput
        {
            Result = new ObjectResult(ToSummary(tag, 0)) { StatusCode = StatusCodes.Status201Created },
            TaggingMessage = TaggingRequest.ForTag(tag.TagUid).Serialize(),
        };
    }

    [Function("UpdateTag")]
    public async Task<HttpWithTaggingOutput> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "tags/{tagUid:guid}")] HttpRequest req,
        Guid tagUid,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadJsonOrDefaultAsync<UpdateTagRequest>(cancellationToken);
        if (body is null)
        {
            return new HttpWithTaggingOutput { Result = new BadRequestResult() };
        }

        var tag = await db.Tags.SingleOrDefaultAsync(t => t.TagUid == tagUid, cancellationToken);
        if (tag is null)
        {
            return new HttpWithTaggingOutput { Result = new NotFoundResult() };
        }

        var embeddingChanged = false;

        if (body.Name is not null)
        {
            if (!TagInput.TryNormalizeName(body.Name, out var name))
            {
                return new HttpWithTaggingOutput { Result = TagInput.InvalidName() };
            }

            if (name != tag.Name)
            {
                if (await db.Tags.AnyAsync(t => t.Name == name && t.Id != tag.Id, cancellationToken))
                {
                    return new HttpWithTaggingOutput { Result = TagInput.NameTaken() };
                }

                tag.Name = name;
                embeddingChanged = true;
            }
        }

        // Null lascia la descrizione invariata, una stringa vuota la toglie.
        if (body.Description is not null)
        {
            if (!TagInput.TryNormalizeDescription(body.Description, out var description))
            {
                return new HttpWithTaggingOutput { Result = TagInput.InvalidDescription() };
            }

            if (description != tag.Description)
            {
                tag.Description = description;
                embeddingChanged = true;
            }
        }

        var reactivated = body.IsActive == true && !tag.IsActive;
        if (body.IsActive is { } isActive)
        {
            tag.IsActive = isActive;
        }

        if (embeddingChanged && !await TrySetEmbeddingAsync(tag, cancellationToken))
        {
            return new HttpWithTaggingOutput { Result = TagInput.EmbeddingUnavailable() };
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("Modifica del tag {TagUid} in conflitto con un aggiornamento concorrente", tagUid);
            return new HttpWithTaggingOutput { Result = new ConflictObjectResult(new { error = "conflict" }) };
        }

        // Un tag disattivato non si applica: lo farà la riattivazione.
        var apply = tag.IsActive && (embeddingChanged || reactivated);
        if (apply)
        {
            logger.LogInformation("Tag {TagUid} modificato e accodato per l'assegnazione", tagUid);
        }

        var recordCount = await db.RecordTags
            .CountAsync(rt => rt.TagId == tag.Id && rt.Source != RecordTagSource.Excluded, cancellationToken);

        return new HttpWithTaggingOutput
        {
            Result = new OkObjectResult(ToSummary(tag, recordCount)),
            TaggingMessage = apply ? TaggingRequest.ForTag(tag.TagUid).Serialize() : null,
        };
    }

    /// <summary>Elimina il tag e tutte le sue assegnazioni ed esclusioni.</summary>
    [Function("DeleteTag")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tags/{tagUid:guid}")] HttpRequest req,
        Guid tagUid,
        CancellationToken cancellationToken)
    {
        var tagId = await db.Tags
            .Where(t => t.TagUid == tagUid)
            .Select(t => (int?)t.Id)
            .SingleOrDefaultAsync(cancellationToken);

        // Già eliminato o di un altro utente: per il client il risultato è lo stesso.
        if (tagId is null)
        {
            return new NoContentResult();
        }

        // Con i retry attivi la transazione esplicita va eseguita nella strategia di esecuzione.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Nessuna cascata da Tags a RecordTags (vedi RecordTagConfiguration). Il tag è dell'utente
            // autenticato, quindi lo sono anche le sue assegnazioni.
            await db.RecordTags
                .IgnoreQueryFilters([MemoaDbContext.CurrentUserFilter])
                .Where(rt => rt.TagId == tagId)
                .ExecuteDeleteAsync(ct);
            await db.Tags.Where(t => t.Id == tagId).ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);
        }, cancellationToken);

        logger.LogInformation("Tag {TagUid} eliminato", tagUid);
        return new NoContentResult();
    }

    /// <summary>False se Azure OpenAI non risponde: il client riceve un errore e nulla viene salvato.</summary>
    private async Task<bool> TrySetEmbeddingAsync(Tag tag, CancellationToken cancellationToken)
    {
        try
        {
            await tag.SetEmbeddingAsync(embeddingClient, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Embedding del tag {TagUid} non calcolato", tag.TagUid);
            return false;
        }
    }

    private static TagSummary ToSummary(Tag tag, int recordCount) =>
        new(tag.TagUid, tag.Name, tag.Description, tag.IsActive, recordCount);
}

public sealed record CreateTagRequest(string? Name, string? Description);

/// <summary>Campi da modificare; quelli null restano invariati.</summary>
public sealed record UpdateTagRequest(string? Name, string? Description, bool? IsActive);

/// <summary>Tag nella pagina dei tag. <paramref name="RecordCount"/> conta i record a cui è assegnato.</summary>
public sealed record TagSummary(Guid TagUid, string Name, string? Description, bool IsActive, int RecordCount);

/// <summary>Tag nella propria pagina, con i record a cui è assegnato.</summary>
public sealed record TagDetail(
    Guid TagUid,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<RecordSummary> Records);
