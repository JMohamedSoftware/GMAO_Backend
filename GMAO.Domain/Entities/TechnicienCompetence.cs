namespace GMAO.Domain.Entities;

public class TechnicienCompetence
{
    public int TechnicienId { get; set; }
    public User Technicien { get; set; } = null!;

    public int CompetenceId { get; set; }
    public Competence Competence { get; set; } = null!;
}
