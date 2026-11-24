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

        entity.Property(d => d.StorageProvider)
              .IsRequired()
              .HasMaxLength(32)
              .HasDefaultValue("Local");

        entity.Property(d => d.BucketName)
              .HasMaxLength(100);

        entity.Property(d => d.Sha256Checksum)
              .HasMaxLength(64);

        entity.HasIndex(d => d.SocieteId);
        entity.HasIndex(d => d.StorageProvider);

        // ── Document → Societe (Many-to-One, Nullable pour rétrocompatibilité) ──
        entity.HasOne(d => d.Societe)
              .WithMany()
              .HasForeignKey(d => d.SocieteId)
              .OnDelete(DeleteBehavior.SetNull);

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

        // ── Document → Incident/DemandeIntervention (Many-to-One, Nullable) ──
        entity.HasOne(d => d.Incident)
              .WithMany(i => i.Documents)
              .HasForeignKey(d => d.IncidentId)
              .OnDelete(DeleteBehavior.SetNull);

        // ── Document → UploadedBy/User (Many-to-One) ──────────────────────────
        entity.HasOne(d => d.UploadedBy)
              .WithMany()
              .HasForeignKey(d => d.UploadedById)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
