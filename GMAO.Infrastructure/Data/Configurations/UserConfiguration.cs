using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("Users");
        entity.HasKey(u => u.Id);

        entity.Property(u => u.Nom)
              .IsRequired()
              .HasMaxLength(100);

        entity.Property(u => u.Prenom)
              .IsRequired()
              .HasMaxLength(100);

        entity.Property(u => u.Email)
              .IsRequired()
              .HasMaxLength(150);

        entity.Property(u => u.PasswordHash)
              .IsRequired();

        entity.Property(u => u.Telephone)
              .HasMaxLength(20);

        entity.HasIndex(u => u.Email).IsUnique();

        // ── User → Role (Many-to-One) ────────────────────────────────────────
        entity.HasOne(u => u.Role)
              .WithMany()
              .HasForeignKey(u => u.RoleId)
              .OnDelete(DeleteBehavior.Restrict);

        // ── User → Societe (Many-to-One) ─────────────────────────────────────
        entity.HasOne(u => u.Societe)
              .WithMany(s => s.Users)
              .HasForeignKey(u => u.SocieteId)
              .OnDelete(DeleteBehavior.SetNull);

        // ── User → DemandesIntervention (One-to-Many) ────────────────────────
        entity.HasMany(u => u.DemandesIntervention)
              .WithOne(d => d.Demandeur)
              .HasForeignKey(d => d.DemandeurId)
              .OnDelete(DeleteBehavior.Restrict);

        // ── User → Notifications (One-to-Many) ──────────────────────────────
        entity.HasMany(u => u.Notifications)
              .WithOne(n => n.User)
              .HasForeignKey(n => n.UserId)
              .OnDelete(DeleteBehavior.Cascade);

        // ── User → Historiques (One-to-Many) ────────────────────────────────
        entity.HasMany(u => u.Historiques)
              .WithOne(h => h.User)
              .HasForeignKey(h => h.UserId)
              .OnDelete(DeleteBehavior.Restrict);

        // ── User → RefreshTokens (One-to-Many) ──────────────────────────────
        entity.HasMany(u => u.RefreshTokens)
              .WithOne(r => r.User)
              .HasForeignKey(r => r.UserId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
