using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class CampagneConfiguration : IEntityTypeConfiguration<Campagne>
{
    public void Configure(EntityTypeBuilder<Campagne> entity)
    {
        entity.ToTable("Campagnes");
        entity.HasKey(c => c.Id);

        entity.Property(c => c.Nom)
              .IsRequired()
              .HasMaxLength(150);

        entity.Property(c => c.Description)
              .HasMaxLength(500);

        // ── Campagne → OrdresTravail (One-to-Many) ───────────────────────────
        entity.HasMany(c => c.OrdresTravail)
              .WithOne(o => o.Campagne)
              .HasForeignKey(o => o.CampagneId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
