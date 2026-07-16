using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.ToTable("RefreshTokens");
        entity.HasKey(r => r.Id);

        entity.Property(r => r.Token)
              .IsRequired()
              .HasMaxLength(500);

        entity.HasIndex(r => r.Token).IsUnique();

        // Relation déjà déclarée dans UserConfiguration
        // (HasMany RefreshTokens → WithOne User)
    }
}
