namespace GMAO.Domain.Entities;

public class SiteUsine
{
    public int Id { get; set; }
    
    public int SocieteId { get; set; }
    public Societe? Societe { get; set; }
    
    public string Nom { get; set; } = string.Empty;
    public string CodeSite { get; set; } = string.Empty;
    public string Ville { get; set; } = string.Empty;
    public string Gouvernorat { get; set; } = string.Empty;
    public string? Adresse { get; set; }
    public string Type { get; set; } = "Usine de Transformation";
    public string Statut { get; set; } = "Opérationnel"; // "Opérationnel", "En maintenance", "Arrêt technique", "En construction"
    public string CapaciteProduction { get; set; } = "500 T/j";
    public int NombreEquipements { get; set; } = 30;
    public int NombreUtilisateurs { get; set; } = 12;
    public string? Responsable { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
