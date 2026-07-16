using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> entity)
    {
        entity.ToTable("Roles");
        entity.HasKey(r => r.Id);

        entity.Property(r => r.Nom)
              .IsRequired()
              .HasMaxLength(50);

        entity.Property(r => r.Description)
              .HasMaxLength(200);

        entity.HasIndex(r => r.Nom).IsUnique();
    }
}
