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
    public double MinSimilarity { get; set; } = 0.35;

    /// <summary>
    /// Dopo quanti minuti un'attesa di assegnazione non conclusa (processo fallito senza ripulire) non rende
    /// più "in assegnazione" i tag. Con l'attesa che parte dall'inizio dell'elaborazione, un audio lungo
    /// trascritto dal ripiego può superarla: i tag tornano allo stato finale prima del confronto.
    /// </summary>
    [Range(1, 1_440)]
    public int PendingTimeoutMinutes { get; set; } = 15;

    public TimeSpan PendingTimeout => TimeSpan.FromMinutes(PendingTimeoutMinutes);
}
