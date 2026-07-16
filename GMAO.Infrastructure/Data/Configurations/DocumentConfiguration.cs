using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> entity)
    {
        entity.ToTable("Documents");
        entity.HasKey(d => d.Id);

        entity.Property(d => d.Nom)
              .IsRequired()
              .HasMaxLength(255);

        entity.Property(d => d.Chemin)
              .IsRequired()
              .HasMaxLength(500);

        entity.Property(d => d.MimeType)
              .HasMaxLength(100);

        // ── Document → Equipement (Many-to-One, Nullable) ────────────────────
        entity.HasOne(d => d.Equipement)
              .WithMany(e => e.Documents)
              .HasForeignKey(d => d.EquipementId)
              .OnDelete(DeleteBehavior.SetNull);

        // ── Document → OrdreTravail (Many-to-One, Nullable) ──────────────────
        entity.HasOne(d => d.OT)
              .WithMany(o => o.Documents)
              .HasForeignKey(d => d.OTId)
              .OnDelete(DeleteBehavior.SetNull);

        // ── Document → PlanPreventif (Many-to-One, Nullable) ──────────────────
        entity.HasOne(d => d.PlanPreventif)
              .WithMany(p => p.Documents)
              .HasForeignKey(d => d.PlanPreventifId)
              .OnDelete(DeleteBehavior.SetNull);

        // ── Document → UploadedBy/User (Many-to-One) ──────────────────────────
        entity.HasOne(d => d.UploadedBy)
              .WithMany()
              .HasForeignKey(d => d.UploadedById)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
