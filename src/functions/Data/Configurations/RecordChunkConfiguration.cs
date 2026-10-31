using Memoa.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoa.Functions.Data.Configurations;

internal sealed class RecordChunkConfiguration : IEntityTypeConfiguration<RecordChunk>
{
    public void Configure(EntityTypeBuilder<RecordChunk> builder)
    {
        // Consegna almeno una volta: l'indicizzazione sostituisce tutti i chunk del record in una
        // transazione; l'indice univoco impedisce duplicati se due esecuzioni si sovrappongono.
        builder.HasOne(c => c.Record)
            .WithMany(r => r.Chunks)
            .HasForeignKey(c => c.RecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.RecordId, c.ChunkIndex }).IsUnique();

        // Ricerca esatta con VECTOR_DISTANCE: nessun indice vettoriale.
        builder.Property(c => c.Embedding).HasColumnType($"vector({RecordChunk.EmbeddingDimensions})");
        builder.Property(c => c.EmbeddingModel).HasMaxLength(100).IsUnicode(false);
    }
}
