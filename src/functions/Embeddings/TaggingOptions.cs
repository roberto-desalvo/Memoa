using System.ComponentModel.DataAnnotations;

namespace Memoa.Functions.Embeddings;

/// <summary>Assegnazione automatica dei tag (sezione "Tagging").</summary>
public sealed class TaggingOptions
{
    public const string SectionName = "Tagging";

    /// <summary>
    /// Similarità coseno minima (1 - distanza) tra il tag e almeno un frammento del record perché il tag venga
    /// assegnato. Valore iniziale per text-embedding-3-small, da tarare sulle note reali come
    /// SearchFunction.MinSimilarity.
    /// </summary>
    [Range(0.0, 1.0)]
    public double MinSimilarity { get; set; } = 0.4;
}
