namespace GMAO.Domain.Entities;

public class Localisation
{
    public int Id { get; set; }
    public int SocieteId { get; set; } = 1;
    public string Nom { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Clé étrangère vers le parent — permet une arborescence (Tree) illimitée.
    /// Ex: Usine → Production → Ligne1 → Pompe
    /// </summary>
    public int? ParentId { get; set; }

    // Navigation
    public Societe? Societe { get; set; }
    public Localisation? Parent { get; set; }
    public ICollection<Localisation> SousLocalisations { get; set; } = new List<Localisation>();
    public ICollection<Equipement> Equipements { get; set; } = new List<Equipement>();
}
