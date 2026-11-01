using System.Diagnostics.CodeAnalysis;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Embeddings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlTypes;
using OpenAI.Embeddings;

namespace Memoa.Functions.Tagging;

/// <summary>Controlli sui tag inviati dal client e calcolo del loro embedding.</summary>
internal static class TagInput
{
    public static bool TryNormalizeName(string? name, [NotNullWhen(true)] out string? normalized)
    {
        normalized = name?.Trim();
        return !string.IsNullOrEmpty(normalized) && normalized.Length <= Tag.MaxNameLength;
    }

    /// <summary>Descrizione facoltativa: vuota diventa null. Mai nei log.</summary>
    public static bool TryNormalizeDescription(string? description, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return normalized is null || normalized.Length <= Tag.MaxDescriptionLength;
    }

    public static IActionResult InvalidName() =>
        new BadRequestObjectResult(new { error = "invalid_name", maxLength = Tag.MaxNameLength });

    public static IActionResult InvalidDescription() =>
        new BadRequestObjectResult(new { error = "invalid_description", maxLength = Tag.MaxDescriptionLength });

    public static IActionResult NameTaken() =>
        new ConflictObjectResult(new { error = "name_taken" });

    /// <summary>Azure OpenAI non ha risposto: nulla è stato salvato e il client può riprovare.</summary>
    public static IActionResult EmbeddingUnavailable() =>
        new ObjectResult(new { error = "embedding_unavailable" }) { StatusCode = StatusCodes.Status503ServiceUnavailable };

    /// <summary>
    /// Calcola l'embedding del tag da nome e descrizione, il testo che i frammenti dei record devono
    /// ricordare perché il tag venga assegnato.
    /// </summary>
    public static async Task SetEmbeddingAsync(this Tag tag, EmbeddingClient client, CancellationToken cancellationToken)
    {
        var text = tag.Description is null ? tag.Name : $"{tag.Name}\n{tag.Description}";
        var embeddings = await client.GenerateCheckedEmbeddingsAsync([text], cancellationToken);
        tag.Embedding = new SqlVector<float>(embeddings[0].ToFloats());
        tag.EmbeddingModel = embeddings.Model;
    }
}
