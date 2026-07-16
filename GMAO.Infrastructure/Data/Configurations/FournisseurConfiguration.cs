using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class FournisseurConfiguration : IEntityTypeConfiguration<Fournisseur>
{
    public void Configure(EntityTypeBuilder<Fournisseur> entity)
    {
        entity.ToTable("Fournisseurs");
        entity.HasKey(f => f.Id);

        entity.Property(f => f.Code)
              .IsRequired()
              .HasMaxLength(20);

        entity.Property(f => f.Nom)
              .IsRequired()
              .HasMaxLength(200);

        entity.Property(f => f.Adresse)
              .HasMaxLength(400);

        entity.Property(f => f.Telephone)
              .HasMaxLength(20);

        entity.Property(f => f.Email)
              .HasMaxLength(150);

        entity.Property(f => f.Contact)
              .HasMaxLength(150);

        entity.HasIndex(f => f.Code).IsUnique();

        // ── Fournisseur → Equipements (One-to-Many) ──────────────────────────
        entity.HasMany(f => f.Equipements)
              .WithOne(e => e.Fournisseur)
              .HasForeignKey(e => e.FournisseurId)
              .OnDelete(DeleteBehavior.SetNull);

        // ── Fournisseur → Pieces (One-to-Many) ───────────────────────────────
        entity.HasMany(f => f.Pieces)
              .WithOne(p => p.Fournisseur)
              .HasForeignKey(p => p.FournisseurId)
              .OnDelete(DeleteBehavior.SetNull);
    }
}
