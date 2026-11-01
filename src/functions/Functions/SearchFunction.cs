using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Embeddings;
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
/// frammenti più simili, per contenuto o per titolo della nota (ricerca esatta con VECTOR_DISTANCE, senza
/// indici vettoriali), ciascuno con il riferimento alla nota. La richiesta arriva nel corpo, mai nell'URL, e non finisce nei log.
/// </summary>
public sealed class SearchFunction(
    MemoaDbContext db,
    EmbeddingClient embeddingClient,
    ILogger<SearchFunction> logger)
{
    public const int MaxQueryLength = 1_000;

    private const int MaxResults = 10;

    /// <summary>
    /// Similarità coseno minima (1 - distanza) del frammento o del titolo perché un frammento sia un
    /// risultato. Valore iniziale per
    /// text-embedding-3-small, da tarare sulle note reali: tra lingue diverse le similarità sono più basse.
    /// </summary>
    private const double MinSimilarity = 0.3;

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
        const double maxDistance = 1 - MinSimilarity;

        // Il filtro globale limita i frammenti a quelli dell'utente autenticato. Si confrontano solo i
        // vettori dello stesso modello: quelli di un modello diverso non sono confrontabili.
        // Due confronti in OR: la richiesta con il frammento e con il titolo della nota. Il titolo conta una
        // volta sola per nota, sul primo frammento, che vale la distanza minore tra le due.
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
                ContentDistance = EF.Functions.VectorDistance("cosine", c.Embedding, vector),
                // Null senza embedding del titolo o se è di un altro modello.
                TitleDistance = c.ChunkIndex == 0 && c.Record.TitleEmbeddingModel == model
                    ? (double?)EF.Functions.VectorDistance("cosine", c.Record.TitleEmbedding!.Value, vector)
                    : null,
            })
            .Select(r => new
            {
                r.Content,
                r.RecordUid,
                r.Title,
                r.OccurredAt,
                r.SourceType,
                Distance = r.TitleDistance < r.ContentDistance ? r.TitleDistance!.Value : r.ContentDistance,
            })
            .Where(r => r.Distance <= maxDistance)
            .OrderBy(r => r.Distance)
            .Take(MaxResults)
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
