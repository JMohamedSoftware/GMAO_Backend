using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class AuditTrailEntryConfiguration : IEntityTypeConfiguration<AuditTrailEntry>
{
    public void Configure(EntityTypeBuilder<AuditTrailEntry> entity)
    {
        entity.ToTable("AuditTrailEntries");
        entity.HasKey(a => a.Id);

        entity.Property(a => a.UserName).HasMaxLength(150);
        entity.Property(a => a.UserRole).HasMaxLength(80);
        entity.Property(a => a.SocieteName).HasMaxLength(150);
        entity.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
        entity.Property(a => a.EntityId).IsRequired().HasMaxLength(100);
        entity.Property(a => a.Action).IsRequired().HasMaxLength(40);
        entity.Property(a => a.BusinessActionLabel).IsRequired().HasMaxLength(200);
        entity.Property(a => a.ChangedProperties).HasMaxLength(1000);
        entity.Property(a => a.TransactionId).HasMaxLength(64);
        entity.Property(a => a.CorrelationId).HasMaxLength(128);
        entity.Property(a => a.Source).IsRequired().HasMaxLength(64);
        entity.Property(a => a.IpAddress).HasMaxLength(64);

        entity.HasIndex(a => a.TimestampUtc);
        entity.HasIndex(a => a.SocieteId);
        entity.HasIndex(a => a.EntityName);
        entity.HasIndex(a => a.TransactionId);
        entity.HasIndex(a => a.UserId);
    }
}
