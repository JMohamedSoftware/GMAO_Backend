using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class SocieteConfiguration : IEntityTypeConfiguration<Societe>
{
    public void Configure(EntityTypeBuilder<Societe> entity)
    {
        entity.ToTable("Societes");
        entity.HasKey(s => s.Id);

        entity.Property(s => s.Nom)
              .IsRequired()
              .HasMaxLength(150);

        entity.Property(s => s.CodeTenant)
              .IsRequired()
              .HasMaxLength(50);
              
        entity.HasIndex(s => s.CodeTenant).IsUnique();

        entity.Property(s => s.Adresse)
              .HasMaxLength(250);

        entity.Property(s => s.EmailContact)
              .HasMaxLength(150);

        // Les relations sont gérées implicitement ou configurées depuis le côté 'Many' 
        // comme dans UserConfiguration.
    }
}
