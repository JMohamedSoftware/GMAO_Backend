namespace GMAO.Domain.Entities;

public class Societe
{
    public int Id { get; set; }
    
    // Identifiant unique utilisé par le Frontend (ex: "tenant-midi")
    public string CodeTenant { get; set; } = string.Empty;
    
    public string Nom { get; set; } = string.Empty;
    
    public string? Adresse { get; set; }
    
    public string? EmailContact { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
}
