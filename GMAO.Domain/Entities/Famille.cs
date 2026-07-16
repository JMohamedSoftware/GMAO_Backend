namespace GMAO.Domain.Entities;

public class Famille
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconeUrl { get; set; }

    // Navigation
    public ICollection<Equipement> Equipements { get; set; } = new List<Equipement>();
}
