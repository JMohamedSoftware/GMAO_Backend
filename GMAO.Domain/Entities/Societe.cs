namespace GMAO.Domain.Entities;

public class Societe
{
    public int Id { get; set; }
    
    // Identifiant unique utilisé par le Frontend (ex: "tenant-midi")
    public string CodeTenant { get; set; } = string.Empty;
    
    public string Nom { get; set; } = string.Empty;
    
    public string? Category { get; set; } = "Agroalimentaire";
    
    public string? ManagerNom { get; set; }
    
    public string? Telephone { get; set; }
    
    public string? Ville { get; set; }
    
    public string? Gouvernorat { get; set; }
    
    public string? Adresse { get; set; }
    
    public string? EmailContact { get; set; }
    
    public string SubscriptionPlan { get; set; } = "Basic"; // "Starter", "Pro", "Enterprise", "Custom"
    
    public string SubscriptionStatus { get; set; } = "Actif"; // "Actif", "En essai", "Suspendu", "Expiré"
    
    public DateTime? SubscriptionStartDate { get; set; } = DateTime.UtcNow.AddMonths(-3);
    
    public DateTime? SubscriptionEndDate { get; set; } = DateTime.UtcNow.AddMonths(9);
    
    public decimal SubscriptionAmount { get; set; } = 850m;
    
    public int MaxSites { get; set; } = 3;
    
    public int MaxUsers { get; set; } = 25;
    
    public int MaxEquipements { get; set; } = 150;
    
    public double StorageUsedGb { get; set; } = 12.5;
    
    public double MaxStorageGb { get; set; } = 50.0;
    
    public string? LogoUrl { get; set; }
    
    public int CapacityTonsPerDay { get; set; } = 450;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<SiteUsine> Sites { get; set; } = new List<SiteUsine>();
    public ICollection<Facture> Factures { get; set; } = new List<Facture>();
}
