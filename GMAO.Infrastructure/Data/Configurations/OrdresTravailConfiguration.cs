using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class OrdresTravailConfiguration : IEntityTypeConfiguration<OrdresTravail>
{
    public void Configure(EntityTypeBuilder<OrdresTravail> entity)
    {
        entity.ToTable("OrdresTravail");
        entity.HasKey(o => o.Id);

        entity.Property(o => o.NumeroOT)
              .IsRequired()
              .HasMaxLength(30);

        entity.Property(o => o.Description)
              .HasMaxLength(1000);

        entity.Property(o => o.Instructions)
              .HasMaxLength(2000);

        // Index unique sur le numéro OT (déjà dans DbContext, centralisé ici)
        entity.HasIndex(o => o.NumeroOT).IsUnique();

        // ── OT → Responsable/User (Many-to-One) ──────────────────────────────
        entity.HasOne(o => o.Responsable)
              .WithMany(u => u.OrdresTravailResponsable)
              .HasForeignKey(o => o.ResponsableId)
              .OnDelete(DeleteBehavior.Restrict);

        // ── OT → Technicien/User (Many-to-One, nullable) ─────────────────────
        entity.HasOne(o => o.Technicien)
              .WithMany(u => u.OrdresTravailTechnicien)
              .HasForeignKey(o => o.TechnicienId)
              .OnDelete(DeleteBehavior.SetNull);

        // ── OT → DemandeIntervention (One-to-One, nullable) ──────────────────
        entity.HasOne(o => o.Demande)
              .WithOne(d => d.OrdreTravail)
              .HasForeignKey<OrdresTravail>(o => o.DemandeId)
              .OnDelete(DeleteBehavior.SetNull);

        // ── OT → Campagne (Many-to-One, nullable) ────────────────────────────
        entity.HasOne(o => o.Campagne)
              .WithMany(c => c.OrdresTravail)
              .HasForeignKey(o => o.CampagneId)
              .OnDelete(DeleteBehavior.SetNull);

        // ── OT → MouvementsStock (One-to-Many) ───────────────────────────────
        entity.HasMany(o => o.MouvementsStock)
              .WithOne(m => m.OT)
              .HasForeignKey(m => m.OTId)
              .OnDelete(DeleteBehavior.SetNull);

        // ── OT → Documents (One-to-Many) ─────────────────────────────────────
        entity.HasMany(o => o.Documents)
              .WithOne(d => d.OT)
              .HasForeignKey(d => d.OTId)
              .OnDelete(DeleteBehavior.SetNull);

        // Précisions décimales
        entity.Property(o => o.CoutMainOeuvre).HasPrecision(18, 2);
        entity.Property(o => o.CoutPieces).HasPrecision(18, 2);
        entity.Property(o => o.CoutSousTraitance).HasPrecision(18, 2);
    }
}
