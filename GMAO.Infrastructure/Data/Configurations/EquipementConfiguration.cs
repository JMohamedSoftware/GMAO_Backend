using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class EquipementConfiguration : IEntityTypeConfiguration<Equipement>
{
    public void Configure(EntityTypeBuilder<Equipement> entity)
    {
        entity.ToTable("Equipements");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Code)
              .IsRequired()
              .HasMaxLength(30);

        entity.Property(e => e.Designation)
              .IsRequired()
              .HasMaxLength(200);

        entity.Property(e => e.Marque)
              .HasMaxLength(100);

        entity.Property(e => e.Modele)
              .HasMaxLength(100);

        entity.Property(e => e.NumeroSerie)
              .HasMaxLength(100);

        entity.Property(e => e.PhotoUrl)
              .HasMaxLength(500);

        entity.Property(e => e.Notes)
              .HasMaxLength(1000);

        // Code unique par équipement
        entity.HasIndex(e => e.Code).IsUnique();

        // ── Equipement → Famille (Many-to-One) ───────────────────────────────
        entity.HasOne(e => e.Famille)
              .WithMany(f => f.Equipements)
              .HasForeignKey(e => e.FamilleId)
              .OnDelete(DeleteBehavior.Restrict);

        // ── Equipement → Localisation (Many-to-One) ──────────────────────────
        // (relation déjà configurée dans LocalisationConfiguration)

        // ── Equipement → DemandesIntervention (One-to-Many) ──────────────────
        entity.HasMany(e => e.DemandesIntervention)
              .WithOne(d => d.Equipement)
              .HasForeignKey(d => d.EquipementId)
              .OnDelete(DeleteBehavior.Restrict);

        // ── Equipement → OrdresTravail (One-to-Many) ─────────────────────────
        entity.HasMany(e => e.OrdresTravail)
              .WithOne(o => o.Equipement)
              .HasForeignKey(o => o.EquipementId)
              .OnDelete(DeleteBehavior.Restrict);

        // ── Equipement → PlansPreventifs (One-to-Many) ───────────────────────
        entity.HasMany(e => e.PlansPreventifs)
              .WithOne(p => p.Equipement)
              .HasForeignKey(p => p.EquipementId)
              .OnDelete(DeleteBehavior.Cascade);

        // ── Equipement → Documents (One-to-Many) ─────────────────────────────
        entity.HasMany(e => e.Documents)
              .WithOne(d => d.Equipement)
              .HasForeignKey(d => d.EquipementId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
