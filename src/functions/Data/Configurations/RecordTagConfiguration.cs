using Memoa.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoa.Functions.Data.Configurations;

internal sealed class RecordTagConfiguration : IEntityTypeConfiguration<RecordTag>
{
    public void Configure(EntityTypeBuilder<RecordTag> builder)
    {
        // Una sola riga per coppia: assegnazioni concorrenti dello stesso tag falliscono e la coda riprova.
        builder.HasKey(rt => new { rt.RecordId, rt.TagId });

        builder.HasOne(rt => rt.Record)
            .WithMany(r => r.Tags)
            .HasForeignKey(rt => rt.RecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // SQL Server non accetta due percorsi di cascata da Users (via Records e via Tags): le righe di un
        // tag si eliminano esplicitamente prima del tag (DeleteTag, DeleteAccount).
        builder.HasOne(rt => rt.Tag)
            .WithMany(t => t.RecordTags)
            .HasForeignKey(rt => rt.TagId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(rt => rt.Source).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
    }
}
