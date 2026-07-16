using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class PieceConfiguration : IEntityTypeConfiguration<Piece>
{
    public void Configure(EntityTypeBuilder<Piece> entity)
    {
        entity.ToTable("Pieces");
        entity.HasKey(p => p.Id);

        entity.Property(p => p.Reference)
              .IsRequired()
              .HasMaxLength(50);

        entity.Property(p => p.Designation)
              .IsRequired()
              .HasMaxLength(200);

        entity.Property(p => p.Unite)
              .HasMaxLength(20);

        entity.Property(p => p.Emplacement)
              .HasMaxLength(100);

        entity.Property(p => p.PrixUnitaire).HasPrecision(18, 4);
        entity.Property(p => p.StockActuel).HasPrecision(18, 3);
        entity.Property(p => p.StockMinimum).HasPrecision(18, 3);
        entity.Property(p => p.StockMaximum).HasPrecision(18, 3);

        entity.HasIndex(p => p.Reference).IsUnique();

        // ── Relations ──
        // FamillePiece → Pieces and Fournisseur → Pieces are already configured in their respective configs.
    }
}
