using Memoa.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoa.Functions.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(u => u.EntraObjectId).IsUnique();

        builder.Property(u => u.Email).HasMaxLength(320);
        builder.Property(u => u.PreferredLanguage).HasMaxLength(5).IsUnicode(false);
    }
}
