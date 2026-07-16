using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class MouvementStockConfiguration : IEntityTypeConfiguration<MouvementStock>
{
    public void Configure(EntityTypeBuilder<MouvementStock> entity)
    {
        entity.ToTable("MouvementsStock");
        entity.HasKey(m => m.Id);

        entity.Property(m => m.Reference)
              .HasMaxLength(100);

        entity.Property(m => m.Motif)
              .HasMaxLength(500);

        entity.Property(m => m.Quantite)
              .HasPrecision(18, 3);

        entity.Property(m => m.PrixUnitaire)
              .HasPrecision(18, 4);

        entity.Property(m => m.PrixTotal)
              .HasPrecision(18, 4);

        // ── MouvementStock → Piece (Many-to-One) ─────────────────────────────
        entity.HasOne(m => m.Piece)
              .WithMany(p => p.MouvementsStock)
              .HasForeignKey(m => m.PieceId)
              .OnDelete(DeleteBehavior.Restrict);

        // ── MouvementStock → User (Many-to-One) ──────────────────────────────
        entity.HasOne(m => m.User)
              .WithMany()
              .HasForeignKey(m => m.UserId)
              .OnDelete(DeleteBehavior.Restrict);

        // MouvementStock → OT is already configured in OrdresTravailConfiguration
    }
}
