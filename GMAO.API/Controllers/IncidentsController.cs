using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.API.Security;
using GMAO.Application.Interfaces;
using GMAO.Application.Security;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    [HasPermission(GmaoPermissions.IncidentView)]
    public async Task<IActionResult> GetAll()
    {
        int societeId = GetSocieteId();
        int currentUserId = GetCurrentUserId();

        var query = _context.DemandesIntervention
            .AsNoTracking()
            .Where(i => i.SocieteId == societeId);

        var scope = GmaoPermissions.GetScope(User, GmaoPermissions.IncidentView);
        if (scope == AccessScope.None)
            return Forbid();

        if (scope == AccessScope.Team)
        {
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId);
            int? teamId = currentUser?.EquipeId;

            query = teamId.HasValue
                ? query.Where(i => i.DemandeurId == currentUserId || (i.Demandeur != null && i.Demandeur.EquipeId == teamId.Value))
                : query.Where(i => i.DemandeurId == currentUserId);
        }
        else if (scope == AccessScope.Own)
        {
            query = query.Where(i => i.DemandeurId == currentUserId);
        }

        var incidents = await query.ToListAsync();
        return Ok(incidents);
    }

    [HttpGet("{id}")]
    [HasPermission(GmaoPermissions.IncidentView)]
    public async Task<IActionResult> GetById(int id)
    {
        int societeId = GetSocieteId();
        int currentUserId = GetCurrentUserId();

        var incident = await _context.DemandesIntervention
            .AsNoTracking()
            .Include(i => i.Demandeur)
            .FirstOrDefaultAsync(i => i.Id == id && i.SocieteId == societeId);

        if (incident == null) return NotFound();

        var scope = GmaoPermissions.GetScope(User, GmaoPermissions.IncidentView);
        if (scope == AccessScope.None)
            return Forbid();

        if (scope == AccessScope.Team)
        {
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId);
            int? teamId = currentUser?.EquipeId;
            bool inSameTeam = teamId.HasValue && incident.Demandeur != null && incident.Demandeur.EquipeId == teamId.Value;
            if (incident.DemandeurId != currentUserId && !inSameTeam)
                return Forbid();
        }
        else if (scope == AccessScope.Own && incident.DemandeurId != currentUserId)
        {
            return Forbid();
        }

        return Ok(incident);
    }

    [HttpPost]
    [HasPermission(GmaoPermissions.IncidentCreate)]
    public async Task<IActionResult> Create([FromBody] DemandeIntervention incident)
    {
        int societeId = GetSocieteId();
        incident.SocieteId = societeId;

        if (incident.EquipementId > 0)
        {
            var eqValid = await _context.Equipements.AsNoTracking()
                .AnyAsync(e => e.Id == incident.EquipementId && e.SocieteId == societeId);
            if (!eqValid)
                return BadRequest("L'équipement spécifié n'existe pas ou n'appartient pas à votre société.");
        }

        if (incident.DemandeurId <= 0)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId > 0)
                incident.DemandeurId = currentUserId;
        }

        await _repository.AddAsync(incident);

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

    [HttpPut("{id}")]
    [HasPermission(GmaoPermissions.IncidentUpdate, GmaoPermissions.IncidentValidate)]
    public async Task<IActionResult> Update(int id, [FromBody] DemandeIntervention incident)
    {
        if (id != incident.Id) return BadRequest("L'id dans l'URL ne correspond pas au corps.");

        int societeId = GetSocieteId();
        int currentUserId = GetCurrentUserId();

        var existing = await _context.DemandesIntervention
            .Include(i => i.Demandeur)
            .FirstOrDefaultAsync(i => i.Id == id && i.SocieteId == societeId);
        if (existing == null) return NotFound();

        var updateScope = GmaoPermissions.GetScope(User, GmaoPermissions.IncidentUpdate);
        bool canValidate = GmaoPermissions.HasPermission(User, GmaoPermissions.IncidentValidate);
        if (updateScope == AccessScope.Own && !canValidate && existing.DemandeurId != currentUserId)
            return Forbid();

        if (updateScope == AccessScope.Team && !canValidate && existing.DemandeurId != currentUserId)
        {
            var teamId = await _context.Users.AsNoTracking()
                .Where(u => u.Id == currentUserId && u.SocieteId == societeId)
                .Select(u => u.EquipeId)
                .FirstOrDefaultAsync();
            bool inSameTeam = teamId.HasValue && existing.Demandeur != null && existing.Demandeur.EquipeId == teamId.Value;
            if (!inSameTeam) return Forbid();
        }

        if (incident.EquipementId > 0 && incident.EquipementId != existing.EquipementId)
        {
            var eqValid = await _context.Equipements.AsNoTracking()
                .AnyAsync(e => e.Id == incident.EquipementId && e.SocieteId == societeId);
            if (!eqValid)
                return BadRequest("L'équipement cible n'existe pas ou n'appartient pas à votre société.");
        }

        if ((incident.Statut == StatutDemande.Validee || incident.Statut == StatutDemande.Rejetee)
            && incident.Statut != existing.Statut
            && !canValidate
            && updateScope != AccessScope.All)
        {
            return Forbid();
        }

        existing.EquipementId     = incident.EquipementId > 0 ? incident.EquipementId : existing.EquipementId;
        existing.DemandeurId      = incident.DemandeurId > 0 ? incident.DemandeurId : existing.DemandeurId;
        existing.DatePanne        = incident.DatePanne;
        existing.Description      = incident.Description;
        existing.Priorite         = incident.Priorite;
        existing.Statut           = incident.Statut;
        existing.PhotoUrl         = incident.PhotoUrl;
        existing.CommentaireRejet = incident.CommentaireRejet;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [HasPermission(GmaoPermissions.IncidentDelete)]
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
