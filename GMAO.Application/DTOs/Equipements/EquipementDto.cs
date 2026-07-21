using System;

namespace GMAO.Application.DTOs.Equipements;

public class EquipementDto
{
    public int Id { get; set; }
    public int SocieteId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public int FamilleId { get; set; }
    public string FamilleNom { get; set; } = string.Empty;
    public int LocalisationId { get; set; }
    public string LocalisationNom { get; set; } = string.Empty;
    public string? Marque { get; set; }
    public string? Modele { get; set; }
    public string? NumeroSerie { get; set; }
    public DateOnly? DateAchat { get; set; }
    public DateOnly? DateMiseEnService { get; set; }
    public DateOnly? DateFinGarantie { get; set; }
    public string Criticite { get; set; } = string.Empty;
    public string Etat { get; set; } = string.Empty;
    public int? FournisseurId { get; set; }
    public string? FournisseurNom { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
