using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class HistoriqueConfiguration : IEntityTypeConfiguration<Historique>
{
    public void Configure(EntityTypeBuilder<Historique> entity)
    {
        entity.ToTable("Historiques");
        entity.HasKey(h => h.Id);

        entity.Property(h => h.Action)
              .IsRequired()
              .HasMaxLength(50);

        entity.Property(h => h.TableConcernee)
              .IsRequired()
              .HasMaxLength(100);

        entity.Property(h => h.EntityId)
              .HasMaxLength(100);

        entity.Property(h => h.AdresseIp)
              .HasMaxLength(50);

        // Relation User → Historiques est configurée dans UserConfiguration
    }
}
