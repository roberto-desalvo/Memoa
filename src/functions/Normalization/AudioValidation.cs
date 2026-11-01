namespace Memoa.Functions.Normalization;

/// <summary>
/// Regole sul formato reale del file (non sull'estensione né sul tipo dichiarato dal client). I codici
/// finiscono in Records.ErrorCode tramite <see cref="Functions.EnqueueTranscriptionFunction"/>.
/// </summary>
public static class AudioValidation
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(30);

    public const string FormatNotSupported = "format_not_supported";
    public const string VideoNotSupported = "video_not_supported";
    public const string NoAudio = "no_audio";
    public const string AudioEmpty = "audio_empty";
    public const string AudioTooLong = "audio_too_long";
    public const string ProcessingFailed = Memoa.Shared.Pipeline.NormalizedAudio.ProcessingFailed;

    /// <summary>
    /// Nomi dei demuxer di ffmpeg (gli stessi del format_name di ffprobe) per MP3, M4A/AAC, WAV, FLAC,
    /// Ogg/Opus, WebM e AMR/3GP. MP4 e 3GP condividono il demuxer mov: i file con video sono esclusi dal
    /// controllo sugli stream.
    /// </summary>
    private static readonly HashSet<string> AllowedFormats =
    [
        "mp3",
        "mov,mp4,m4a,3gp,3g2,mj2",
        "aac",
        "wav",
        "flac",
        "ogg",
        "matroska,webm",
        "amr",
    ];

    /// <summary>Controlli sul file caricato, prima della conversione. Null se il file è accettato.</summary>
    public static string? ValidateSource(AudioProbe probe)
    {
        if (!AllowedFormats.Contains(probe.FormatName))
        {
            return FormatNotSupported;
        }

        if (probe.HasVideo)
        {
            return VideoNotSupported;
        }

        if (probe.AudioStreams == 0)
        {
            return NoAudio;
        }

        // Se la durata non è nell'header si verifica sulla copia normalizzata.
        return probe.DurationSeconds > MaxDuration.TotalSeconds ? AudioTooLong : null;
    }

    /// <summary>Controlli sulla copia normalizzata, che ha sempre una durata affidabile.</summary>
    public static string? ValidateNormalized(AudioProbe probe) => probe.DurationSeconds switch
    {
        null or <= 0 => AudioEmpty,
        var seconds when seconds > MaxDuration.TotalSeconds => AudioTooLong,
        _ => null,
    };
}
