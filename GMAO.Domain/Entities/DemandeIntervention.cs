using GMAO.Domain.Enums;

namespace GMAO.Domain.Entities;

public class DemandeIntervention
{
    public int Id { get; set; }
    public int EquipementId { get; set; }
    public int DemandeurId { get; set; }
    public DateTime DatePanne { get; set; }
    public string Description { get; set; } = string.Empty;
    public PrioriteIntervention Priorite { get; set; } = PrioriteIntervention.Normale;
    public StatutDemande Statut { get; set; } = StatutDemande.EnAttente;
    public string? PhotoUrl { get; set; }
    public string? CommentaireRejet { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Equipement Equipement { get; set; } = null!;
    public User Demandeur { get; set; } = null!;
    public OrdresTravail? OrdreTravail { get; set; }
}
