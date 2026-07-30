using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Domain.Interfaces;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdresTravailController : ControllerBase
{
    private readonly IGenericRepository<OrdresTravail> _repository;
    private readonly IGenericRepository<DemandeIntervention> _incidentRepository;

    public OrdresTravailController(
        IGenericRepository<OrdresTravail> repository,
        IGenericRepository<DemandeIntervention> incidentRepository)
    {
        _repository = repository;
        _incidentRepository = incidentRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var ordres = await _repository.GetAllAsync();
        return Ok(ordres);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var ordre = await _repository.GetByIdAsync(id);
        if (ordre == null) return NotFound();
        return Ok(ordre);
    }

    /// <summary>
    /// Creates a new work order. If demandeId is provided, also sets the
    /// corresponding incident status to TransformeeEnOT (4).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrdresTravail ordre)
    {
        await _repository.AddAsync(ordre);

        // Auto-transition the linked incident to "Transformée en OT"
        if (ordre.DemandeId.HasValue)
        {
            var incident = await _incidentRepository.GetByIdAsync(ordre.DemandeId.Value);
            if (incident != null)
            {
                incident.Statut = StatutDemande.TransformeeEnOT;
                await _incidentRepository.UpdateAsync(incident);
            }
        }

        return CreatedAtAction(nameof(GetById), new { id = ordre.Id }, ordre);
    }

    /// <summary>
    /// Updates a work order. Copies scalar fields onto the tracked entity to avoid
    /// EF Core "duplicate tracking" InvalidOperationException.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] OrdresTravail ordre)
    {
        if (id != ordre.Id) return BadRequest("L'id dans l'URL ne correspond pas au corps.");

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        // Patch only scalar/value properties
        existing.NumeroOT          = ordre.NumeroOT;
        existing.DemandeId         = ordre.DemandeId;
        existing.EquipementId      = ordre.EquipementId;
        existing.ResponsableId     = ordre.ResponsableId;
        existing.TechnicienId      = ordre.TechnicienId;
        existing.Priorite          = ordre.Priorite;
        existing.TypeMaintenance   = ordre.TypeMaintenance;
        existing.Statut            = ordre.Statut;
        existing.DateDebutPrevue   = ordre.DateDebutPrevue;
        existing.DateFinPrevue     = ordre.DateFinPrevue;
        existing.DateDebutReelle   = ordre.DateDebutReelle;
        existing.DateFinReelle     = ordre.DateFinReelle;
        existing.Description       = ordre.Description;
        existing.Instructions      = ordre.Instructions;
        existing.CoutMainOeuvre    = ordre.CoutMainOeuvre;
        existing.CoutPieces        = ordre.CoutPieces;
        existing.CoutSousTraitance = ordre.CoutSousTraitance;
        existing.CampagneId        = ordre.CampagneId;

        await _repository.UpdateAsync(existing);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ordre = await _repository.GetByIdAsync(id);
        if (ordre == null) return NotFound();

        await _repository.DeleteAsync(ordre);
        return NoContent();
    }
}
