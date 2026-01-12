using Memoa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoa.Infrastructure.Data
{
    public class MemoaDbContext : DbContext
    {
        public MemoaDbContext(DbContextOptions<MemoaDbContext> options)
            : base(options)
        {
        }

        public DbSet<Entry> Entries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurazione Entry
            modelBuilder.Entity<Entry>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .ValueGeneratedNever(); // Il client genera l'ID

                entity.Property(e => e.UserId)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.Text)
                    .IsRequired()
                    .HasColumnType("text")
                    .HasMaxLength(100000);

                entity.Property(e => e.CreatedAt)
                    .IsRequired()
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.UpdatedAt)
                    .IsRequired(false);

                entity.Property(e => e.AudioTranscription)
                    .IsRequired(false)
                    .HasColumnType("text");

                // Indice per query per userId
                entity.HasIndex(e => e.UserId);
            });
        }
    }
}
