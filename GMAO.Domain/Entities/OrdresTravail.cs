using GMAO.Domain.Enums;

namespace GMAO.Domain.Entities;

public class OrdresTravail
{
    public int Id { get; set; }
    public string NumeroOT { get; set; } = string.Empty;  // Ex: OT-2026-001
    public int? DemandeId { get; set; }
    public int EquipementId { get; set; }
    public int ResponsableId { get; set; }
    public int? TechnicienId { get; set; }
    public PrioriteIntervention Priorite { get; set; } = PrioriteIntervention.Normale;
    public TypeMaintenance TypeMaintenance { get; set; } = TypeMaintenance.Corrective;
    public StatutOT Statut { get; set; } = StatutOT.Cree;
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public DateTime? DateDebutPrevue { get; set; }
    public DateTime? DateFinPrevue { get; set; }
    public DateTime? DateDebutReelle { get; set; }
    public DateTime? DateFinReelle { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public decimal? CoutMainOeuvre { get; set; }
    public decimal? CoutPieces { get; set; }
    public decimal? CoutSousTraitance { get; set; }
    public int? CampagneId { get; set; }

    // Navigation
    public DemandeIntervention? Demande { get; set; }
    public Equipement Equipement { get; set; } = null!;
    public User Responsable { get; set; } = null!;
    public User? Technicien { get; set; }
    public Campagne? Campagne { get; set; }
    public Intervention? Intervention { get; set; }
    public ICollection<MouvementStock> MouvementsStock { get; set; } = new List<MouvementStock>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
