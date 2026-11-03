namespace Memoa.Functions.Data.Entities;

/// <summary>
/// Parametri di sistema modificabili dagli amministratori (<see cref="User.IsAdmin"/>), in un'unica riga
/// con <see cref="Id"/> = <see cref="SingletonId"/>. Ogni valore null vale "usa la configurazione":
/// i valori effettivi li calcola <see cref="Settings.SystemSettingsProvider"/>.
/// </summary>
public sealed class SystemSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; }

    /// <summary>Sostituisce Speech:Enabled.</summary>
    public bool? SpeechEnabled { get; set; }

    /// <summary>Sostituisce Speech:BaseTimeoutSeconds.</summary>
    public int? SpeechBaseTimeoutSeconds { get; set; }

    /// <summary>Sostituisce Speech:TimeoutPerAudioMinuteSeconds.</summary>
    public int? SpeechTimeoutPerAudioMinuteSeconds { get; set; }

    /// <summary>Sostituisce Search:MinSimilarity.</summary>
    public double? SearchMinSimilarity { get; set; }

    /// <summary>Sostituisce Search:MaxResults.</summary>
    public int? SearchMaxResults { get; set; }

    /// <summary>Sostituisce Tagging:MinSimilarity.</summary>
    public double? TaggingMinSimilarity { get; set; }

    /// <summary>Sostituisce Tagging:PendingTimeoutMinutes.</summary>
    public int? TaggingPendingTimeoutMinutes { get; set; }

    public DateTime UpdatedAt { get; set; }
}
