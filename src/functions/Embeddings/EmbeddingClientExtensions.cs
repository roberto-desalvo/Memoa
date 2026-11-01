using Memoa.Functions.Data.Entities;
using OpenAI.Embeddings;

namespace Memoa.Functions.Embeddings;

/// <summary>Calcolo degli embedding comune a indicizzazione, ricerca e tag.</summary>
internal static class EmbeddingClientExtensions
{
    /// <summary>
    /// Un embedding per ogni testo, con le dimensioni delle colonne vector. La collezione riporta il modello
    /// restituito dal servizio (non il nome del deployment), da salvare accanto ai vettori. Il client riprova
    /// da sé sugli errori temporanei e sui 429.
    /// </summary>
    public static async Task<OpenAIEmbeddingCollection> GenerateCheckedEmbeddingsAsync(
        this EmbeddingClient client, IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        var embeddings = (await client.GenerateEmbeddingsAsync(
            inputs,
            new EmbeddingGenerationOptions { Dimensions = RecordChunk.EmbeddingDimensions },
            cancellationToken)).Value;

        if (embeddings.Count != inputs.Count || string.IsNullOrEmpty(embeddings.Model))
        {
            throw new InvalidOperationException("Risposta di embedding inattesa");
        }

        return embeddings;
    }
}
