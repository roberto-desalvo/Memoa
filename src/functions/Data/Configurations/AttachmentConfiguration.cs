using Memoa.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoa.Functions.Data.Configurations;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        // La cascata elimina solo le righe: i file su Blob Storage vanno cancellati a parte.
        // L'indice su RecordId è creato per convenzione sulla chiave esterna.
        builder.HasOne(a => a.Record)
            .WithMany(r => r.Attachments)
            .HasForeignKey(a => a.RecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(a => a.OriginalBlobPath).HasMaxLength(1024);
        builder.Property(a => a.OriginalContentType).HasMaxLength(100).IsUnicode(false);
        builder.Property(a => a.NormalizedBlobPath).HasMaxLength(1024);
    }
}
