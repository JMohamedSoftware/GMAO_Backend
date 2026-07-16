using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class LocalisationConfiguration : IEntityTypeConfiguration<Localisation>
{
    public void Configure(EntityTypeBuilder<Localisation> entity)
    {
        entity.ToTable("Localisations");
        entity.HasKey(l => l.Id);

        entity.Property(l => l.Nom)
              .IsRequired()
              .HasMaxLength(150);

        entity.Property(l => l.Description)
              .HasMaxLength(400);

        // ── Auto-référence : Localisation → Parent (One-to-Many) ─────────────
        // Déjà configurée dans GmaoDbContext mais on centralise ici.
        entity.HasOne(l => l.Parent)
              .WithMany(l => l.SousLocalisations)
              .HasForeignKey(l => l.ParentId)
              .OnDelete(DeleteBehavior.Restrict);

        // ── Localisation → Equipements (One-to-Many) ─────────────────────────
        entity.HasMany(l => l.Equipements)
              .WithOne(e => e.Localisation)
              .HasForeignKey(e => e.LocalisationId)
              .OnDelete(DeleteBehavior.Restrict);
    }
}
