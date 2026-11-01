using Microsoft.Data.SqlTypes;

namespace Memoa.Functions.Data.Entities;

/// <summary>
/// Tag dell'utente, assegnato ai record a mano o in automatico per similarità tra il suo embedding e
/// i frammenti del record.
/// </summary>
public sealed class Tag
{
    /// <summary>Lunghezza massima del nome (colonna Tags.Name).</summary>
    public const int MaxNameLength = 50;

    /// <summary>Lunghezza massima della descrizione (colonna Tags.Description).</summary>
    public const int MaxDescriptionLength = 2_000;

    public int Id { get; set; }

    /// <summary>Identificativo pubblico, generato nel codice: identifica il tag nelle code e verso il client.</summary>
    public Guid TagUid { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Univoco per utente. Dato personale: mai nei log.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Cosa rappresenta il tag, usata con il nome per l'embedding. Dato personale: mai nei log.</summary>
    public string? Description { get; set; }

    /// <summary>Un tag disattivato non viene più assegnato in automatico; le assegnazioni esistenti restano.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Embedding di nome e descrizione, ricalcolato quando cambiano.</summary>
    public SqlVector<float> Embedding { get; set; }

    /// <summary>Modello che ha prodotto il vettore: si confronta solo con chunk dello stesso modello.</summary>
    public string EmbeddingModel { get; set; } = string.Empty;

    /// <summary>
    /// Da quando il tag attende di essere applicato a tutti i record (creazione, modifica dell'embedding,
    /// riattivazione); azzerato da TagRecordsFunction. Vedi <see cref="Tagging.TagStatus"/>.
    /// </summary>
    public DateTime? AssignmentPendingSince { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Concorrenza ottimistica tra modifiche dell'utente.</summary>
    public byte[] RowVersion { get; set; } = [];

    public List<RecordTag> RecordTags { get; set; } = [];
}
