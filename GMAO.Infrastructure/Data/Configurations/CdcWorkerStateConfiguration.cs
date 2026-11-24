using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class CdcWorkerStateConfiguration : IEntityTypeConfiguration<CdcWorkerState>
{
    public void Configure(EntityTypeBuilder<CdcWorkerState> entity)
    {
        entity.ToTable("CdcWorkerStates");
        entity.HasKey(w => w.Id);

        entity.Property(w => w.SlotName).IsRequired().HasMaxLength(128);
        entity.Property(w => w.PublicationName).IsRequired().HasMaxLength(128);
        entity.Property(w => w.Status).IsRequired().HasMaxLength(64);
        entity.Property(w => w.StatusReason).HasMaxLength(1000);
        entity.Property(w => w.PostgresVersion).HasMaxLength(200);
        entity.Property(w => w.WalLevel).HasMaxLength(32);
        entity.Property(w => w.LastConsumedLsn).HasMaxLength(64);
        entity.Property(w => w.CurrentServerWalLsn).HasMaxLength(64);
        entity.Property(w => w.WalStatus).HasMaxLength(32);

        entity.HasIndex(w => w.SlotName).IsUnique();
    }
}
