using System.ComponentModel.DataAnnotations;

namespace Memoa.Functions.Embeddings;

/// <summary>
/// Ricerca semantica (sezione "Search"). Valori predefiniti, sostituibili dalla pagina di amministrazione
/// (<see cref="Settings.SystemSettingsProvider"/>).
/// </summary>
public sealed class SearchOptions
{
    public const string SectionName = "Search";

    /// <summary>
    /// Similarità coseno minima (1 - distanza) perché un frammento sia un risultato. Valore iniziale per
    /// text-embedding-3-small, da tarare sulle note reali: tra lingue diverse le similarità sono più basse.
    /// </summary>
    [Range(0.0, 1.0)]
    public double MinSimilarity { get; set; } = 0.3;

    /// <summary>Numero massimo di frammenti restituiti.</summary>
    [Range(1, 50)]
    public int MaxResults { get; set; } = 10;
}
