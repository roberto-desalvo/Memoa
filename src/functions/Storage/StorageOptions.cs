using System.ComponentModel.DataAnnotations;

namespace Memoa.Functions.Storage;

/// <summary>
/// Endpoint dello storage account dei dati utente (sezione "DataStorage", la stessa connessione usata
/// dai trigger). L'autenticazione è sempre Entra ID: in Azure l'identità user-assigned indicata da
/// AZURE_CLIENT_ID, in locale l'utente di Azure CLI o Visual Studio.
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

/// <summary>Nomi dei contenitori dello storage dei dati (sezione "Containers").</summary>
public sealed class ContainerOptions
{
    public const string SectionName = "Containers";

    /// <summary>Originali caricati o registrati dal client.</summary>
    [Required]
    public string Uploads { get; set; } = string.Empty;

    /// <summary>Copie normalizzate ed esiti di errore scritti da NormalizeRecord.</summary>
    [Required]
    public string Audio { get; set; } = string.Empty;

    /// <summary>
    /// Trascrizioni ed esiti di errore scritti dal job di trascrizione di ripiego. Le trascrizioni di Azure
    /// AI Speech vanno direttamente nel database.
    /// </summary>
    [Required]
    public string Transcripts { get; set; } = string.Empty;
}

/// <summary>
/// Nomi delle code scritte senza binding di output (sezione "Queues"). Le altre code sono indicate solo
/// nei binding, come "%Queues:&lt;Nome&gt;%".
/// </summary>
public sealed class QueueOptions
{
    public const string SectionName = "Queues";

    /// <summary>Messaggi ritardati che controllano gli upload audio mai completati.</summary>
    [Required]
    public string UploadExpiry { get; set; } = string.Empty;
}
