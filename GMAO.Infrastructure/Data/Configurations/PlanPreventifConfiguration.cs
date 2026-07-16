using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class PlanPreventifConfiguration : IEntityTypeConfiguration<PlanPreventif>
{
    public void Configure(EntityTypeBuilder<PlanPreventif> entity)
    {
        entity.ToTable("PlansPreventifs");
        entity.HasKey(p => p.Id);

        entity.Property(p => p.Titre)
              .IsRequired()
              .HasMaxLength(200);

        entity.Property(p => p.Description)
              .HasMaxLength(1000);

        entity.Property(p => p.UniteMesure)
              .HasMaxLength(50);

        // ── PlanPreventif → TachesPreventives (One-to-Many) ───────────────────
        entity.HasMany(p => p.Taches)
              .WithOne(t => t.Plan)
              .HasForeignKey(t => t.PlanId)
              .OnDelete(DeleteBehavior.Cascade);

        // Equipement → PlansPreventifs is configured in EquipementConfiguration
    }
}
