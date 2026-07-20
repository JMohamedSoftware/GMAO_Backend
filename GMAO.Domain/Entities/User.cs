namespace GMAO.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Telephone { get; set; }
    public int RoleId { get; set; }
    
    // The company the user belongs to
    public int? SocieteId { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Role Role { get; set; } = null!;
    public Societe? Societe { get; set; }
    public ICollection<DemandeIntervention> DemandesIntervention { get; set; } = new List<DemandeIntervention>();
    public ICollection<OrdresTravail> OrdresTravailResponsable { get; set; } = new List<OrdresTravail>();
    public ICollection<OrdresTravail> OrdresTravailTechnicien { get; set; } = new List<OrdresTravail>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Historique> Historiques { get; set; } = new List<Historique>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<TechnicienCompetence> TechnicienCompetences { get; set; } = new List<TechnicienCompetence>();
}
