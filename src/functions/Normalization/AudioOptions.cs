using System.ComponentModel.DataAnnotations;

namespace Memoa.Functions.Normalization;

/// <summary>
/// Limiti degli audio (sezione "Audio"). In Azure il valore viene da Bicep (maxAudioDurationMinutes) e la
/// pipeline del frontend lo copia in config.json, così browser e normalizzazione applicano lo stesso limite.
/// </summary>
public sealed class AudioOptions
{
    public const string SectionName = "Audio";

    /// <summary>Durata massima di un audio in minuti, per la registrazione e per i file caricati.</summary>
    [Range(1, 600)]
    public int MaxDurationMinutes { get; set; }

    public TimeSpan MaxDuration => TimeSpan.FromMinutes(MaxDurationMinutes);
}
