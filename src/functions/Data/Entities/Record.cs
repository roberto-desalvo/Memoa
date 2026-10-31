namespace Memoa.Functions.Data.Entities;

/// <summary>Record del diario, comune a tutte le origini.</summary>
public sealed class Record
{
    /// <summary>Generato nel codice prima del salvataggio: compare anche nel percorso del blob.</summary>
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

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

    /// <summary>Data della nota (UTC), usata per l'ordine cronologico.</summary>
    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Concorrenza ottimistica tra modifiche dell'utente e Function di indicizzazione.</summary>
    public byte[] RowVersion { get; set; } = [];

    public List<Attachment> Attachments { get; set; } = [];

    public List<RecordChunk> Chunks { get; set; } = [];
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
