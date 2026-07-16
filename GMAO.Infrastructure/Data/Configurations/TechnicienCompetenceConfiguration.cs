using GMAO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GMAO.Infrastructure.Data.Configurations;

public class TechnicienCompetenceConfiguration : IEntityTypeConfiguration<TechnicienCompetence>
{
    public void Configure(EntityTypeBuilder<TechnicienCompetence> entity)
    {
        entity.ToTable("TechnicienCompetences");
        entity.HasKey(tc => new { tc.TechnicienId, tc.CompetenceId });

        // ── Technicien & Competences (Many-to-Many join configuration) ────────
        entity.HasOne(tc => tc.Technicien)
              .WithMany(u => u.TechnicienCompetences)
              .HasForeignKey(tc => tc.TechnicienId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(tc => tc.Competence)
              .WithMany(c => c.TechnicienCompetences)
              .HasForeignKey(tc => tc.CompetenceId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
