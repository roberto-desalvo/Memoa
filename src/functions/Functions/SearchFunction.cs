using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Embeddings;
using Memoa.Functions.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace Memoa.Functions.Functions;

/// <summary>
/// Ricerca semantica tra le note dell'utente: calcola l'embedding della richiesta e restituisce i
/// frammenti più simili (ricerca esatta con VECTOR_DISTANCE, senza indici vettoriali), ciascuno con il
/// riferimento alla nota. La richiesta arriva nel corpo, mai nell'URL, e non finisce nei log.
/// </summary>
public sealed class SearchFunction(
    MemoaDbContext db,
    EmbeddingClient embeddingClient,
    SystemSettingsProvider systemSettings,
    ILogger<SearchFunction> logger)
{
    public const int MaxQueryLength = 1_000;

    [Function("Search")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "search")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadJsonOrDefaultAsync<SearchRequest>(cancellationToken);
        var query = body?.Query?.Trim();
        if (string.IsNullOrEmpty(query) || query.Length > MaxQueryLength)
        {
            return new BadRequestObjectResult(new { error = "invalid_query", maxLength = MaxQueryLength });
        }

        // Come in IndexRecordFunction: la collezione riporta il modello che ha prodotto il vettore.
        var embeddings = await embeddingClient.GenerateCheckedEmbeddingsAsync([query], cancellationToken);

        var model = embeddings.Model;
        var vector = new SqlVector<float>(embeddings[0].ToFloats());
        // Similarità minima e numero di risultati: SearchOptions o la pagina di amministrazione.
        var settings = await systemSettings.GetAsync(cancellationToken);
        var maxDistance = 1 - settings.SearchMinSimilarity;

        // Il filtro globale limita i frammenti a quelli dell'utente autenticato. Si confrontano solo i
        // vettori dello stesso modello: quelli di un modello diverso non sono confrontabili.
        var rows = await db.RecordChunks
            .AsNoTracking()
            .Where(c => c.EmbeddingModel == model)
            .Select(c => new
            {
                c.Content,
                c.Record.RecordUid,
                c.Record.Title,
                c.Record.OccurredAt,
                c.Record.SourceType,
                Distance = EF.Functions.VectorDistance("cosine", c.Embedding, vector),
            })
            .Where(r => r.Distance <= maxDistance)
            .OrderBy(r => r.Distance)
            .Take(settings.SearchMaxResults)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Ricerca completata: {ResultCount} risultati", rows.Count);

        var results = rows.Select(r => new SearchResult(
            r.RecordUid,
            r.Title,
            RecordResponses.AsUtc(r.OccurredAt),
            r.SourceType.ToString(),
            r.Content));

        return new OkObjectResult(results);
    }
}

public sealed record SearchRequest(string? Query);

/// <summary>Frammento simile alla richiesta, con il riferimento alla nota da cui proviene.</summary>
public sealed record SearchResult(
    Guid RecordUid,
    string Title,
    DateTimeOffset OccurredAt,
    string SourceType,
    string Content);
