namespace Memoa.Functions.Data.Entities;

/// <summary>
/// File collegato a un record. Le colonne specifiche di un tipo sono nullable.
/// </summary>
public sealed class Attachment
{
    public int Id { get; set; }

    public int RecordId { get; set; }

    public Record Record { get; set; } = null!;

    public AttachmentKind Kind { get; set; }

    /// <summary>File caricato o registrato, mai cancellato né sovrascritto.</summary>
    public string OriginalBlobPath { get; set; } = string.Empty;

    public string OriginalContentType { get; set; } = string.Empty;

    public long OriginalSizeBytes { get; set; }

    /// <summary>Copia normalizzata in Opus (solo audio).</summary>
    public string? NormalizedBlobPath { get; set; }

    /// <summary>Solo audio; scritta da una Function, non da NormalizeRecord.</summary>
    public int? DurationSeconds { get; set; }

    public DateTime CreatedAt { get; set; }
}

public enum AttachmentKind
{
    Audio,
    Image,
}
