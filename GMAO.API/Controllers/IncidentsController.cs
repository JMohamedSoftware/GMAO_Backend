using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Domain.Enums;
using GMAO.Application.Interfaces;
using GMAO.Infrastructure.Data;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IncidentsController : ControllerBase
{
    private readonly IGenericRepository<DemandeIntervention> _repository;
    private readonly GmaoDbContext _context;
    private readonly ITenantContextService _tenantContext;

    public IncidentsController(
        IGenericRepository<DemandeIntervention> repository,
        GmaoDbContext context,
        ITenantContextService tenantContext)
    {
        _repository = repository;
        _context = context;
        _tenantContext = tenantContext;
    }

    private int GetSocieteId() => _tenantContext.GetRequiredTenantId();

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("id")?.Value 
            ?? User.FindFirst("sub")?.Value;
        return int.TryParse(claim, out int uid) ? uid : 0;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        int societeId = GetSocieteId();
        int currentUserId = GetCurrentUserId();

        var query = _context.DemandesIntervention
            .AsNoTracking()
            .Where(i => i.SocieteId == societeId);

        // RBAC Scoping: SuperAdmin, Admin, CompanyAdmin, or INCIDENTS_VIEW_ALL can see all
        bool isFullAccess = User.IsInRole("SuperAdmin") || 
                            User.IsInRole("Administrateur") || 
                            User.IsInRole("CompanyAdmin") ||
                            User.HasClaim("Permission", "INCIDENTS_VIEW_ALL") ||
                            User.HasClaim("Permission", "INCIDENTS_VIEW");

        if (!isFullAccess)
        {
            if (User.HasClaim("Permission", "INCIDENTS_VIEW_TEAM"))
            {
                var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId);
                int? teamId = currentUser?.EquipeId;

                if (teamId.HasValue)
                {
                    query = query.Where(i => i.DemandeurId == currentUserId || (i.Demandeur != null && i.Demandeur.EquipeId == teamId.Value));
                }
                else
                {
                    query = query.Where(i => i.DemandeurId == currentUserId);
                }
            }
            else
            {
                // Default / INCIDENTS_VIEW_OWN: only see own requests
                query = query.Where(i => i.DemandeurId == currentUserId);
            }
        }

        var incidents = await query.ToListAsync();
        return Ok(incidents);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        int societeId = GetSocieteId();
        int currentUserId = GetCurrentUserId();

        var incident = await _context.DemandesIntervention
            .AsNoTracking()
            .Include(i => i.Demandeur)
            .FirstOrDefaultAsync(i => i.Id == id && i.SocieteId == societeId);

        if (incident == null) return NotFound();

        // RBAC Scope Check
        bool isFullAccess = User.IsInRole("SuperAdmin") || 
                            User.IsInRole("Administrateur") || 
                            User.IsInRole("CompanyAdmin") ||
                            User.HasClaim("Permission", "INCIDENTS_VIEW_ALL") ||
                            User.HasClaim("Permission", "INCIDENTS_VIEW");

        if (!isFullAccess)
        {
            if (User.HasClaim("Permission", "INCIDENTS_VIEW_TEAM"))
            {
                var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId);
                int? teamId = currentUser?.EquipeId;
                bool inSameTeam = teamId.HasValue && incident.Demandeur != null && incident.Demandeur.EquipeId == teamId.Value;
                if (incident.DemandeurId != currentUserId && !inSameTeam)
                {
                    return Forbid();
                }
            }
            else if (incident.DemandeurId != currentUserId)
            {
                return Forbid();
            }
        }

        return Ok(incident);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DemandeIntervention incident)
    {
        int societeId = GetSocieteId();
        incident.SocieteId = societeId;

        // Auto-assign authenticated user as Demandeur if not set or <= 0
        if (incident.DemandeurId <= 0)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId > 0)
            {
                incident.DemandeurId = currentUserId;
            }
        }

        await _repository.AddAsync(incident);

        // If priority is Haute or Critique, set equipment to EnPanne
        if (incident.EquipementId > 0 && (int)incident.Priorite >= (int)PrioriteIntervention.Haute)
        {
            var eq = await _context.Equipements.FirstOrDefaultAsync(e => e.Id == incident.EquipementId && e.SocieteId == societeId);
            if (eq != null)
            {
                eq.Etat = EtatEquipement.EnPanne;
                eq.UpdatedAt = DateTime.UtcNow;
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

        int societeId = GetSocieteId();
        var existing = await _context.DemandesIntervention.FirstOrDefaultAsync(i => i.Id == id && i.SocieteId == societeId);
        if (existing == null) return NotFound();

        // Patch only the scalar/value properties — do NOT replace navigation objects
        existing.EquipementId      = incident.EquipementId;
        existing.DemandeurId       = incident.DemandeurId > 0 ? incident.DemandeurId : existing.DemandeurId;
        existing.DatePanne         = incident.DatePanne;
        existing.Description       = incident.Description;
        existing.Priorite          = incident.Priorite;
        existing.Statut            = incident.Statut;
        existing.PhotoUrl          = incident.PhotoUrl;
        existing.CommentaireRejet  = incident.CommentaireRejet;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        int societeId = GetSocieteId();
        var incident = await _context.DemandesIntervention.FirstOrDefaultAsync(i => i.Id == id && i.SocieteId == societeId);
        if (incident == null) return NotFound();
        
        _context.DemandesIntervention.Remove(incident);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
