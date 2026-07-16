using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class TachePreventiveConfiguration : IEntityTypeConfiguration<TachePreventive>
{
    public void Configure(EntityTypeBuilder<TachePreventive> entity)
    {
        entity.ToTable("TachesPreventives");
        entity.HasKey(t => t.Id);

        entity.Property(t => t.Description)
              .IsRequired()
              .HasMaxLength(500);

        // Relation Plan → Taches is already configured in PlanPreventifConfiguration
    }
}
