using System;
using System.Collections.Generic;

namespace GMAO.Application.DTOs.Equipements;

public class EquipementDto
{
    public int Id { get; set; }
    public int SocieteId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public int FamilleId { get; set; }
    public string FamilleNom { get; set; } = string.Empty;
    public string Category => FamilleNom;
    public int? LocalisationId { get; set; }
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
    public List<int> PiecesIds { get; set; } = new List<int>();
    public List<GMAO.Application.DTOs.Documents.DocumentDto> Documents { get; set; } = new();
}

public class EquipementInputDto
{
    public object? Id { get; set; }
    public string? Code { get; set; }
    public string? Designation { get; set; }
    public string? Name { get; set; }
    public object? FamilleId { get; set; }
    public string? FamilleNom { get; set; }
    public string? Category { get; set; }
    public object? LocalisationId { get; set; }
    public string? Marque { get; set; }
    public string? Brand { get; set; }
    public string? Modele { get; set; }
    public string? Model { get; set; }
    public string? NumeroSerie { get; set; }
    public string? SerialNumber { get; set; }
    public object? DateAchat { get; set; }
    public object? DateMiseEnService { get; set; }
    public object? DateFinGarantie { get; set; }
    public object? Criticite { get; set; }
    public object? Criticality { get; set; }
    public object? Etat { get; set; }
    public object? Status { get; set; }
    public object? FournisseurId { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Notes { get; set; }
}
