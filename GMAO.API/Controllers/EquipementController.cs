using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Application.DTOs.Equipements;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EquipementController : ControllerBase
{
    private readonly IEquipementRepository _equipementRepository;

    public EquipementController(IEquipementRepository equipementRepository)
    {
        _equipementRepository = equipementRepository;
    }

    private int GetSocieteId()
    {
        var societeIdClaim = User.FindFirst("SocieteId")?.Value;
        if (int.TryParse(societeIdClaim, out int societeId))
            return societeId;
        return 0; // Or throw exception
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var societeId = GetSocieteId();
        var equipements = await _equipementRepository.GetAllWithDetailsAsync(societeId);
        
        var dtos = equipements.Select(e => new EquipementDto
        {
            Id = e.Id,
            SocieteId = e.SocieteId,
            Code = e.Code,
            Designation = e.Designation,
            FamilleId = e.FamilleId,
            FamilleNom = e.Famille?.Nom ?? "",
            LocalisationId = e.LocalisationId,
            LocalisationNom = e.Localisation?.Nom ?? "",
            Marque = e.Marque,
            Modele = e.Modele,
            NumeroSerie = e.NumeroSerie,
            DateAchat = e.DateAchat,
            DateMiseEnService = e.DateMiseEnService,
            DateFinGarantie = e.DateFinGarantie,
            Criticite = e.Criticite.ToString(),
            Etat = e.Etat.ToString(),
            FournisseurId = e.FournisseurId,
            FournisseurNom = e.Fournisseur?.Nom,
            PhotoUrl = e.PhotoUrl,
            Notes = e.Notes,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        });

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var societeId = GetSocieteId();
        var e = await _equipementRepository.GetByIdWithDetailsAsync(id, societeId);
        if (e == null) return NotFound();

        var dto = new EquipementDto
        {
            Id = e.Id,
            SocieteId = e.SocieteId,
            Code = e.Code,
            Designation = e.Designation,
            FamilleId = e.FamilleId,
            FamilleNom = e.Famille?.Nom ?? "",
            LocalisationId = e.LocalisationId,
            LocalisationNom = e.Localisation?.Nom ?? "",
            Marque = e.Marque,
            Modele = e.Modele,
            NumeroSerie = e.NumeroSerie,
            DateAchat = e.DateAchat,
            DateMiseEnService = e.DateMiseEnService,
            DateFinGarantie = e.DateFinGarantie,
            Criticite = e.Criticite.ToString(),
            Etat = e.Etat.ToString(),
            FournisseurId = e.FournisseurId,
            FournisseurNom = e.Fournisseur?.Nom,
            PhotoUrl = e.PhotoUrl,
            Notes = e.Notes,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };

        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Equipement equipement)
    {
        var societeId = GetSocieteId();
        equipement.SocieteId = societeId;
        
        await _equipementRepository.AddAsync(equipement);
        return CreatedAtAction(nameof(GetById), new { id = equipement.Id }, equipement);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Equipement equipement)
    {
        if (id != equipement.Id) return BadRequest();
        
        var societeId = GetSocieteId();
        var existing = await _equipementRepository.GetByIdAsync(id);
        if (existing == null || existing.SocieteId != societeId) return NotFound();

        equipement.SocieteId = societeId;
        await _equipementRepository.UpdateAsync(equipement);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var societeId = GetSocieteId();
        var equipement = await _equipementRepository.GetByIdAsync(id);
        if (equipement == null || equipement.SocieteId != societeId) return NotFound();
        
        await _equipementRepository.DeleteAsync(equipement);
        return NoContent();
    }
}
