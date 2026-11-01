using Memoa.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoa.Functions.Data.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasOne(t => t.User)
            .WithMany(u => u.Tags)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TagUid).IsUnique();

        // Nome univoco per utente (con la collation del database, senza distinguere le maiuscole).
        builder.HasIndex(t => new { t.UserId, t.Name }).IsUnique();

        builder.Property(t => t.Name).HasMaxLength(Tag.MaxNameLength);
        builder.Property(t => t.Description).HasMaxLength(Tag.MaxDescriptionLength);
        builder.Property(t => t.Embedding).HasColumnType($"vector({RecordChunk.EmbeddingDimensions})");
        builder.Property(t => t.NameEmbedding).HasColumnType($"vector({RecordChunk.EmbeddingDimensions})");
        builder.Property(t => t.EmbeddingModel).HasMaxLength(100).IsUnicode(false);
        builder.Property(t => t.RowVersion).IsRowVersion();
    }
}
