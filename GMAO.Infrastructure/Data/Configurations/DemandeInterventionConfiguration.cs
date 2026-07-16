using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class DemandeInterventionConfiguration : IEntityTypeConfiguration<DemandeIntervention>
{
    public void Configure(EntityTypeBuilder<DemandeIntervention> entity)
    {
        entity.ToTable("DemandesIntervention");
        entity.HasKey(d => d.Id);

        entity.Property(d => d.Description)
              .IsRequired()
              .HasMaxLength(1000);

        entity.Property(d => d.PhotoUrl)
              .HasMaxLength(500);

        entity.Property(d => d.CommentaireRejet)
              .HasMaxLength(500);

        // ── DemandeIntervention → Equipement (Many-to-One) ───────────────────
        // Déjà déclarée dans EquipementConfiguration

        // ── DemandeIntervention → Demandeur/User (Many-to-One) ───────────────
        // Déjà déclarée dans UserConfiguration
    }
}
