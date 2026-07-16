using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class FamilleConfiguration : IEntityTypeConfiguration<Famille>
{
    public void Configure(EntityTypeBuilder<Famille> entity)
    {
        entity.ToTable("Familles");
        entity.HasKey(f => f.Id);

        entity.Property(f => f.Nom)
              .IsRequired()
              .HasMaxLength(100);

        entity.Property(f => f.Description)
              .HasMaxLength(300);

        entity.Property(f => f.IconeUrl)
              .HasMaxLength(500);

        entity.HasIndex(f => f.Nom).IsUnique();
    }
}
