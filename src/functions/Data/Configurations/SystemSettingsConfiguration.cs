using Memoa.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoa.Functions.Data.Configurations;

internal sealed class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        // Una sola riga, creata al primo salvataggio dalla pagina di amministrazione.
        builder.ToTable(t => t.HasCheckConstraint("CK_SystemSettings_Singleton", $"[Id] = {SystemSettings.SingletonId}"));
        builder.Property(s => s.Id).ValueGeneratedNever();
    }
}
