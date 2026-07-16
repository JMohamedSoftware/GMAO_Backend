using GMAO.Domain.Enums;

namespace GMAO.Domain.Entities;

public class Equipement
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public int FamilleId { get; set; }
    public int LocalisationId { get; set; }
    public string? Marque { get; set; }
    public string? Modele { get; set; }
    public string? NumeroSerie { get; set; }
    public DateOnly? DateAchat { get; set; }
    public DateOnly? DateMiseEnService { get; set; }
    public DateOnly? DateFinGarantie { get; set; }
    public CriticiteEquipement Criticite { get; set; } = CriticiteEquipement.Moyenne;
    public EtatEquipement Etat { get; set; } = EtatEquipement.EnService;
    public int? FournisseurId { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Famille Famille { get; set; } = null!;
    public Localisation Localisation { get; set; } = null!;
    public Fournisseur? Fournisseur { get; set; }
    public ICollection<DemandeIntervention> DemandesIntervention { get; set; } = new List<DemandeIntervention>();
    public ICollection<OrdresTravail> OrdresTravail { get; set; } = new List<OrdresTravail>();
    public ICollection<PlanPreventif> PlansPreventifs { get; set; } = new List<PlanPreventif>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
