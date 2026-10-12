namespace GMAO.Domain.Entities;

public class PlanOffre
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // "starter", "pro", "enterprise", "custom"
    public string Nom { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PrixMensuel { get; set; }
    public decimal PrixAnnuel { get; set; }
    public int MaxSites { get; set; }
    public int MaxUsers { get; set; }
    public int MaxEquipements { get; set; }
    public int MaxStorageGb { get; set; }
    public string SupportLevel { get; set; } = "Standard";
    public string FonctionnalitesJson { get; set; } = "[]";
    public bool IsPopular { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
