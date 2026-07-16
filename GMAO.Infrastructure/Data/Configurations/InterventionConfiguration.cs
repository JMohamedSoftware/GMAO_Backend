using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class InterventionConfiguration : IEntityTypeConfiguration<Intervention>
{
    public void Configure(EntityTypeBuilder<Intervention> entity)
    {
        entity.ToTable("Interventions");
        entity.HasKey(i => i.Id);

        entity.Property(i => i.Diagnostic)
              .HasMaxLength(1000);

        entity.Property(i => i.CausePanne)
              .HasMaxLength(1000);

        entity.Property(i => i.Solution)
              .HasMaxLength(1000);

        entity.Property(i => i.Observation)
              .HasMaxLength(1000);

        entity.Property(i => i.TempsPasse)
              .HasPrecision(18, 2);

        // ── OT → Intervention (1:1) ──────────────────────────────────────────
        entity.HasOne(i => i.OT)
              .WithOne(o => o.Intervention)
              .HasForeignKey<Intervention>(i => i.OTId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
