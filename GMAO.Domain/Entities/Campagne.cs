using GMAO.Domain.Enums;

namespace GMAO.Domain.Entities;

public class Campagne
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;       // Ex: "Campagne Tomates 2026"
    public string? Description { get; set; }
    public DateOnly DateDebut { get; set; }
    public DateOnly DateFin { get; set; }
    public EtatCampagne Etat { get; set; } = EtatCampagne.Planifiee;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<OrdresTravail> OrdresTravail { get; set; } = new List<OrdresTravail>();
}
