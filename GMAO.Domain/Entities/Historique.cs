namespace GMAO.Domain.Entities;

public class Historique
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; } = string.Empty;     // Ex: "Modifier", "Créer", "Supprimer"
    public string TableConcernee { get; set; } = string.Empty;  // Ex: "OrdresTravail"
    public string? EntityId { get; set; }                  // ID de l'enregistrement modifié
    public string? AnciennesValeurs { get; set; }          // JSON de l'état avant
    public string? NouvellesValeurs { get; set; }          // JSON de l'état après
    public string? AdresseIp { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
