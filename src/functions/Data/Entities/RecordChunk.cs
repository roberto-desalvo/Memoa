using Microsoft.Data.SqlTypes;

namespace Memoa.Functions.Data.Entities;

/// <summary>Frammento del testo di un record con il relativo embedding.</summary>
public sealed class RecordChunk
{
    /// <summary>Dimensioni di text-embedding-3-small.</summary>
    public const int EmbeddingDimensions = 1536;

    public Guid Id { get; set; }

    public Guid RecordId { get; set; }

    public Record Record { get; set; } = null!;

    /// <summary>Posizione del frammento nel testo.</summary>
    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public SqlVector<float> Embedding { get; set; }

    /// <summary>Modello che ha prodotto il vettore: cambiarlo richiede di ricalcolare tutti i vettori.</summary>
    public string EmbeddingModel { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
