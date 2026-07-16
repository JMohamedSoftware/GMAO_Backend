using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class CompetenceConfiguration : IEntityTypeConfiguration<Competence>
{
    public void Configure(EntityTypeBuilder<Competence> entity)
    {
        entity.ToTable("Competences");
        entity.HasKey(c => c.Id);

        entity.Property(c => c.Nom)
              .IsRequired()
              .HasMaxLength(100);

        entity.Property(c => c.Description)
              .HasMaxLength(300);

        entity.HasIndex(c => c.Nom).IsUnique();
    }
}
