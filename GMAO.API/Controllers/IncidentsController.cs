using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Domain.Enums;
using GMAO.Infrastructure.Data;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IncidentsController : ControllerBase
{
    private readonly IGenericRepository<DemandeIntervention> _repository;
    private readonly GmaoDbContext _context;

    public IncidentsController(
        IGenericRepository<DemandeIntervention> repository,
        GmaoDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    private int GetSocieteId()
    {
        var tenantHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (int.TryParse(tenantHeader, out int tenantId) && tenantId > 0)
            return tenantId;

        var societeIdClaim = User.FindFirst("SocieteId")?.Value;
        if (int.TryParse(societeIdClaim, out int societeId) && societeId > 0)
            return societeId;
        return 1;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        int societeId = GetSocieteId();
        var incidents = await _context.DemandesIntervention
            .Where(i => i.SocieteId == societeId)
            .ToListAsync();
        return Ok(incidents);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        int societeId = GetSocieteId();
        var incident = await _context.DemandesIntervention
            .FirstOrDefaultAsync(i => i.Id == id && i.SocieteId == societeId);
        if (incident == null) return NotFound();
        return Ok(incident);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DemandeIntervention incident)
    {
        int societeId = GetSocieteId();
        incident.SocieteId = societeId;
        await _repository.AddAsync(incident);

        // If priority is Haute or Critique, set equipment to EnPanne
        if (incident.EquipementId > 0 && (int)incident.Priorite >= (int)PrioriteIntervention.Haute)
        {
            var eq = await _context.Equipements.FirstOrDefaultAsync(e => e.Id == incident.EquipementId && e.SocieteId == societeId);
            if (eq != null)
            {
                eq.Etat = EtatEquipement.EnPanne;
                eq.UpdatedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

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
