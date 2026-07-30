using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Domain.Enums;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IncidentsController : ControllerBase
{
    private readonly IGenericRepository<DemandeIntervention> _repository;

    public IncidentsController(IGenericRepository<DemandeIntervention> repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var incidents = await _repository.GetAllAsync();
        return Ok(incidents);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var incident = await _repository.GetByIdAsync(id);
        if (incident == null) return NotFound();
        return Ok(incident);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DemandeIntervention incident)
    {
        await _repository.AddAsync(incident);
        return CreatedAtAction(nameof(GetById), new { id = incident.Id }, incident);
    }

    /// <summary>
    /// Updates an incident. Copies scalar fields onto the tracked entity to avoid
    /// EF Core "duplicate tracking" InvalidOperationException.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] DemandeIntervention incident)
    {
        if (id != incident.Id) return BadRequest("L'id dans l'URL ne correspond pas au corps.");

        // Load the already-tracked entity from the DB
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        // Patch only the scalar/value properties — do NOT replace navigation objects
        existing.EquipementId      = incident.EquipementId;
        existing.DemandeurId       = incident.DemandeurId;
        existing.DatePanne         = incident.DatePanne;
        existing.Description       = incident.Description;
        existing.Priorite          = incident.Priorite;
        existing.Statut            = incident.Statut;
        existing.PhotoUrl          = incident.PhotoUrl;
        existing.CommentaireRejet  = incident.CommentaireRejet;

        // existing is already tracked, so Update() will pick up changes
        await _repository.UpdateAsync(existing);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var incident = await _repository.GetByIdAsync(id);
        if (incident == null) return NotFound();
        
        await _repository.DeleteAsync(incident);
        return NoContent();
    }
}
