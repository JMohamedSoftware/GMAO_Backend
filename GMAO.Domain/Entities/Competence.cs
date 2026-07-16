using System.Collections.Generic;

namespace GMAO.Domain.Entities;

public class Competence
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation
    public ICollection<TechnicienCompetence> TechnicienCompetences { get; set; } = new List<TechnicienCompetence>();
}
