using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> entity)
    {
        entity.ToTable("Notifications");
        entity.HasKey(n => n.Id);

        entity.Property(n => n.Titre)
              .IsRequired()
              .HasMaxLength(150);

        entity.Property(n => n.Message)
              .IsRequired()
              .HasMaxLength(500);

        entity.Property(n => n.LienUrl)
              .HasMaxLength(255);

        // Relation User → Notifications is configured in UserConfiguration
    }
}
