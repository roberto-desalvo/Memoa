using Memoa.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoa.Functions.Data.Configurations;

internal sealed class RecordConfiguration : IEntityTypeConfiguration<Record>
{
    public void Configure(EntityTypeBuilder<Record> builder)
    {
        builder.HasOne(r => r.User)
            .WithMany(u => u.Records)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Vista elenco.
        builder.HasIndex(r => new { r.UserId, r.OccurredAt });

        builder.Property(r => r.SourceType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(r => r.ErrorCode).HasMaxLength(50).IsUnicode(false);
        builder.Property(r => r.Language).HasMaxLength(5).IsUnicode(false);
        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}
