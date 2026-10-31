using System.ComponentModel.DataAnnotations;

namespace Memoa.Normalizer;

/// <summary>
/// Endpoint dello storage account dei dati utente (sezione "DataStorage"). L'autenticazione è sempre
/// Entra ID: in Azure l'identità user-assigned indicata da AZURE_CLIENT_ID, in locale l'utente di
/// Azure CLI o Visual Studio.
/// </summary>
public sealed class DataStorageOptions
{
    public const string SectionName = "DataStorage";

    /// <summary>Es. https://&lt;account&gt;.blob.core.windows.net/.</summary>
    [Required]
    public Uri? BlobServiceUri { get; set; }

    /// <summary>Es. https://&lt;account&gt;.queue.core.windows.net/.</summary>
    [Required]
    public Uri? QueueServiceUri { get; set; }
}

/// <summary>Configurazione del job (sezione "Job", impostata da infra/modules/queue-job.bicep).</summary>
public sealed class JobOptions
{
    public const string SectionName = "Job";

    /// <summary>Coda di normalizzazione: l'unica letta dal job.</summary>
    [Required]
    public string Queue { get; set; } = string.Empty;

    /// <summary>Contenitore degli originali (sola lettura).</summary>
    [Required]
    public string InputContainer { get; set; } = string.Empty;

    /// <summary>Contenitore dell'audio normalizzato, l'unico in cui il job scrive.</summary>
    [Required]
    public string OutputContainer { get; set; } = string.Empty;

    /// <summary>
    /// Tentativi oltre i quali il messaggio viene scartato con l'esito "processing_failed": il job non
    /// può scrivere sulla coda dei messaggi non elaborabili.
    /// </summary>
    [Range(1, 10)]
    public int MaxDequeueCount { get; set; } = 3;

    /// <summary>
    /// Deve superare la durata massima di un'esecuzione (replicaTimeout, 30 minuti), altrimenti il
    /// messaggio torna visibile mentre è ancora in elaborazione.
    /// </summary>
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(35);

    public string FfmpegPath { get; set; } = "ffmpeg";

    public string FfprobePath { get; set; } = "ffprobe";
}
