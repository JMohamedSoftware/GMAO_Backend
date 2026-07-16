using GMAO.Domain.Enums;

namespace GMAO.Domain.Entities;

public class PlanPreventif
{
    public int Id { get; set; }
    public int EquipementId { get; set; }
    public string Titre { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TypeDeclenchement TypeDeclenchement { get; set; } = TypeDeclenchement.Periodique;
    public int Frequence { get; set; }           // Ex: 30 (jours) ou 500 (heures)
    public string? UniteMesure { get; set; }     // "jours", "heures", "km"
    public DateTime? DerniereDate { get; set; }
    public DateTime? ProchaineDate { get; set; }
    public bool Actif { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Equipement Equipement { get; set; } = null!;
    public ICollection<TachePreventive> Taches { get; set; } = new List<TachePreventive>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
