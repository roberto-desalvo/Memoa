namespace Memoa.Functions.Data.Entities;

/// <summary>Record del diario, comune a tutte le origini.</summary>
public sealed class Record
{
    /// <summary>Lunghezza massima del titolo (colonna Records.Title).</summary>
    public const int MaxTitleLength = 200;

    public int Id { get; set; }

    /// <summary>
    /// Identificativo pubblico, generato nel codice prima del salvataggio: compare nel percorso del blob
    /// e identifica il record nella pipeline di elaborazione e verso il client.
    /// </summary>
    public Guid RecordUid { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Titolo mostrato nella lista, scelto dall'utente. Dato personale: mai nei log.</summary>
    public string Title { get; set; } = string.Empty;

    public SourceType SourceType { get; set; }

    public RecordStatus Status { get; set; }

    /// <summary>Solo un codice, mai contenuti.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Testo scritto o trascrizione corrente; dopo una modifica i chunk vanno ricalcolati.</summary>
    public string? Text { get; set; }

    /// <summary>Trascrizione automatica, mai modificata. Null per i record di testo.</summary>
    public string? OriginalTranscript { get; set; }

    /// <summary>Lingua rilevata (<see cref="Languages"/>).</summary>
    public string? Language { get; set; }

    /// <summary>Data e ora della nota (UTC), scelte dall'utente e usate per l'ordine cronologico.</summary>
    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Da quando il record attende il confronto con i tag: impostato quando entra in elaborazione, azzerato
    /// da TagRecordsFunction dopo il confronto. Finché è recente (<see cref="Tagging.TagStatus"/>) i tag attivi
    /// dell'utente risultano "in assegnazione".
    /// </summary>
    public DateTime? TagsPendingSince { get; set; }

    /// <summary>Concorrenza ottimistica tra modifiche dell'utente e Function di indicizzazione.</summary>
    public byte[] RowVersion { get; set; } = [];

    public List<Attachment> Attachments { get; set; } = [];

    public List<RecordChunk> Chunks { get; set; } = [];

    /// <summary>Tag assegnati ed esclusi (<see cref="RecordTagSource"/>).</summary>
    public List<RecordTag> Tags { get; set; } = [];
}

public enum SourceType
{
    Audio,
    Text,
    Image,
}

public enum RecordStatus
{
    Processing,
    Ready,
    Error,
}
