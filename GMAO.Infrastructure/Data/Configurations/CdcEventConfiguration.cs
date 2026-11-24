using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class CdcEventConfiguration : IEntityTypeConfiguration<CdcEvent>
{
    public void Configure(EntityTypeBuilder<CdcEvent> entity)
    {
        entity.ToTable("CdcEvents");
        entity.HasKey(c => c.Id);

        entity.Property(c => c.Operation).IsRequired().HasMaxLength(20);
        entity.Property(c => c.SchemaName).IsRequired().HasMaxLength(64);
        entity.Property(c => c.TableName).IsRequired().HasMaxLength(128);
        entity.Property(c => c.RecordId).HasMaxLength(100);
        entity.Property(c => c.TransactionId).IsRequired().HasMaxLength(64);
        entity.Property(c => c.Lsn).IsRequired().HasMaxLength(64);
        entity.Property(c => c.SlotName).IsRequired().HasMaxLength(128);
        entity.Property(c => c.PublicationName).IsRequired().HasMaxLength(128);
        entity.Property(c => c.Source).IsRequired().HasMaxLength(64);

        entity.HasIndex(c => c.CommitTimestampUtc);
        entity.HasIndex(c => c.TableName);
        entity.HasIndex(c => c.TransactionId);
        entity.HasIndex(c => c.SocieteId);
        // Déduplication idempotente en cas de reconnexion/reprise depuis un LSN
        entity.HasIndex(c => new { c.Lsn, c.TableName, c.Operation, c.RecordId }).IsUnique();
    }
}
