using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class FamillePieceConfiguration : IEntityTypeConfiguration<FamillePiece>
{
    public void Configure(EntityTypeBuilder<FamillePiece> entity)
    {
        entity.ToTable("FamillesPieces");
        entity.HasKey(f => f.Id);

        entity.Property(f => f.Nom)
              .IsRequired()
              .HasMaxLength(100);

        entity.HasIndex(f => f.Nom).IsUnique();

        // ── FamillePiece → Pieces (One-to-Many) ──────────────────────────────
        entity.HasMany(f => f.Pieces)
              .WithOne(p => p.FamillePiece)
              .HasForeignKey(p => p.FamillePieceId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
