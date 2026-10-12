namespace GMAO.Domain.Entities;

public class JournalSuperAdmin
{
    public int Id { get; set; }
    public DateTime DateHeure { get; set; } = DateTime.UtcNow;
    public string Niveau { get; set; } = "INFO"; // "INFO", "WARNING", "ERROR", "SECURITY"
    public string Action { get; set; } = string.Empty;
    public string Categorie { get; set; } = "Système"; // "Auth", "Tenant", "Système", "Billing", "Sécurité"
    public int? UserId { get; set; }
    public string UserNom { get; set; } = string.Empty;
    public string UserRole { get; set; } = "SuperAdmin";
    public string SocieteNom { get; set; } = "Plateforme Centrale";
    public string? AdresseIp { get; set; } = "127.0.0.1";
    public string? Navigateur { get; set; } = "Chrome 122.0 (Windows)";
    public string Details { get; set; } = string.Empty;
    public string Statut { get; set; } = "Succès"; // "Succès", "Échec", "Avertissement"
}
