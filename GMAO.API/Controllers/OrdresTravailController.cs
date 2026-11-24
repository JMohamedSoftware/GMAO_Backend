using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Domain.Interfaces;
using GMAO.Application.Interfaces;
using GMAO.Infrastructure.Data;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdresTravailController : ControllerBase
{
    private readonly IGenericRepository<OrdresTravail> _repository;
    private readonly IGenericRepository<DemandeIntervention> _incidentRepository;
    private readonly GmaoDbContext _context;
    private readonly ITenantContextService _tenantContext;

    public OrdresTravailController(
        IGenericRepository<OrdresTravail> repository,
        IGenericRepository<DemandeIntervention> incidentRepository,
        GmaoDbContext context,
        ITenantContextService tenantContext)
    {
        _repository = repository;
        _incidentRepository = incidentRepository;
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

    private static DateTime? ToUtc(DateTime? d)
    {
        if (d == null) return null;
        return d.Value.Kind switch
        {
            DateTimeKind.Utc   => d,
            DateTimeKind.Local => d.Value.ToUniversalTime(),
            _                  => DateTime.SpecifyKind(d.Value, DateTimeKind.Utc)
        };
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        int societeId = GetSocieteId();
        int currentUserId = GetCurrentUserId();

        var query = _context.OrdresTravail
            .AsNoTracking()
            .Include(o => o.Campagne)
            .Include(o => o.Technicien)
            .Include(o => o.Responsable)
            .Where(o => o.SocieteId == societeId);

        // RBAC Scoping
        bool isFullAccess = User.IsInRole("SuperAdmin") || 
                            User.IsInRole("Administrateur") || 
                            User.IsInRole("CompanyAdmin") ||
                            User.HasClaim("Permission", "WORKORDER_VIEW_ALL") ||
                            User.HasClaim("Permission", "WORKORDER_VIEW");

        if (!isFullAccess)
        {
            if (User.HasClaim("Permission", "WORKORDER_VIEW_TEAM"))
            {
                var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId);
                int? teamId = currentUser?.EquipeId;

                if (teamId.HasValue)
                {
                    query = query.Where(ot => 
                        ot.TechnicienId == currentUserId || 
                        ot.ResponsableId == currentUserId ||
                        (ot.Technicien != null && ot.Technicien.EquipeId == teamId.Value) ||
                        (ot.Responsable != null && ot.Responsable.EquipeId == teamId.Value));
                }
                else
                {
                    query = query.Where(ot => ot.TechnicienId == currentUserId || ot.ResponsableId == currentUserId);
                }
            }
            else
            {
                // Default / WORKORDER_VIEW_OWN: only see own work orders
                query = query.Where(ot => ot.TechnicienId == currentUserId || ot.ResponsableId == currentUserId);
            }
        }

        var ordres = await query.ToListAsync();
        return Ok(ordres);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        int societeId = GetSocieteId();
        int currentUserId = GetCurrentUserId();

        var ordre = await _context.OrdresTravail
            .AsNoTracking()
            .Include(o => o.Campagne)
            .Include(o => o.Technicien)
            .Include(o => o.Responsable)
            .FirstOrDefaultAsync(o => o.Id == id && o.SocieteId == societeId);

        if (ordre == null) return NotFound();

        // RBAC Scope Check
        bool isFullAccess = User.IsInRole("SuperAdmin") || 
                            User.IsInRole("Administrateur") || 
                            User.IsInRole("CompanyAdmin") ||
                            User.HasClaim("Permission", "WORKORDER_VIEW_ALL") ||
                            User.HasClaim("Permission", "WORKORDER_VIEW");

        if (!isFullAccess)
        {
            if (User.HasClaim("Permission", "WORKORDER_VIEW_TEAM"))
            {
                var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId);
                int? teamId = currentUser?.EquipeId;
                bool isUserTeam = teamId.HasValue && (
                    (ordre.Technicien != null && ordre.Technicien.EquipeId == teamId.Value) ||
                    (ordre.Responsable != null && ordre.Responsable.EquipeId == teamId.Value));

                if (ordre.TechnicienId != currentUserId && ordre.ResponsableId != currentUserId && !isUserTeam)
                {
                    return Forbid();
                }
            }
            else if (ordre.TechnicienId != currentUserId && ordre.ResponsableId != currentUserId)
            {
                return Forbid();
            }
        }

        return Ok(ordre);
    }

    /// <summary>
    /// Creates a new work order. Auto-generates a clean sequential NumeroOT
    /// (e.g. OT-2026-S1-001) scoped to the company and globally collision-free.
    /// If demandeId is provided, safely handles already existing OTs and
    /// auto-transitions the incident to TransformeeEnOT.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrdresTravail ordre)
    {
        int societeId = GetSocieteId();
        ordre.SocieteId = societeId;

        // If DemandeId already has an OT in the database, return it instead of failing on unique constraint
        if (ordre.DemandeId.HasValue && ordre.DemandeId.Value > 0)
        {
            var existingForDemande = await _context.OrdresTravail
                .FirstOrDefaultAsync(o => o.DemandeId == ordre.DemandeId.Value && o.SocieteId == societeId);
            if (existingForDemande != null)
            {
                return Ok(existingForDemande);
            }
        }

        // Auto-generate clean sequential NumeroOT per company and per year (OT-YYYY-0001, OT-YYYY-0002, ...)
        int currentYear = DateTime.UtcNow.Year;
        string yearPrefix = $"OT-{currentYear}-";

        var existingOts = await _context.OrdresTravail
            .Where(o => o.SocieteId == societeId && o.NumeroOT.StartsWith(yearPrefix))
            .ToListAsync();
        int maxSeq = 0;
        foreach (var o in existingOts)
        {
            if (!string.IsNullOrEmpty(o.NumeroOT))
            {
                var parts = o.NumeroOT.Split('-');
                if (parts.Length >= 3 && int.TryParse(parts[^1], out int s) && s > maxSeq)
                {
                    maxSeq = s;
                }
            }
        }

        // Generate a collision-free NumeroOT for this company (e.g. OT-2026-1, OT-2026-2)
        int seq = maxSeq + 1;
        string candidate;
        do
        {
            candidate = $"{yearPrefix}{seq}";
            seq++;
        } while (await _context.OrdresTravail.AnyAsync(o => o.SocieteId == societeId && o.NumeroOT == candidate));

        ordre.NumeroOT = candidate;
        ordre.DateCreation = DateTime.UtcNow;

        // Auto-assigner la CampagneId active de l'entreprise si non spécifiée
        if (!ordre.CampagneId.HasValue || ordre.CampagneId.Value <= 0)
        {
            var activeCampagneId = await _context.Campagnes
                .Where(c => c.SocieteId == societeId && c.Etat == EtatCampagne.EnCours)
                .OrderByDescending(c => c.DateDebut)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();

            if (!activeCampagneId.HasValue)
            {
                activeCampagneId = await _context.Campagnes
                    .Where(c => c.SocieteId == societeId)
                    .OrderByDescending(c => c.DateDebut)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync();
            }

            ordre.CampagneId = activeCampagneId;
        }

        await _repository.AddAsync(ordre);

        // Auto-transition the linked incident to "Transformée en OT"
        if (ordre.DemandeId.HasValue)
        {
            var incident = await _incidentRepository.GetByIdAsync(ordre.DemandeId.Value);
            if (incident != null && incident.SocieteId == societeId)
            {
                incident.Statut = StatutDemande.TransformeeEnOT;
                await _incidentRepository.UpdateAsync(incident);
            }
        }

        // Auto-update equipment status: if Corrective or high/urgent priority, mark as EnPanne
        if (ordre.EquipementId > 0 && (ordre.TypeMaintenance == TypeMaintenance.Corrective || (int)ordre.Priorite >= (int)PrioriteIntervention.Haute))
        {
            var equipement = await _context.Equipements.FirstOrDefaultAsync(e => e.Id == ordre.EquipementId && e.SocieteId == societeId);
            if (equipement != null)
            {
                equipement.Etat = EtatEquipement.EnPanne;
                equipement.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        return CreatedAtAction(nameof(GetById), new { id = ordre.Id }, ordre);
    }

    /// <summary>
    /// Updates a work order. Validates OT State Machine transitions and permissions:
    /// - Terminal states (Cloture, Annule) cannot be mutated unless SuperAdmin.
    /// - Transitioning to Cloture (6) requires managerial authorization.
    /// - Real start/end dates follow lifecycle.
    /// - Equipment status is automatically updated according to OT lifecycle.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] OrdresTravail ordre)
    {
        if (id != ordre.Id) return BadRequest("L'id dans l'URL ne correspond pas au corps.");

        int societeId = GetSocieteId();
        var existing = await _context.OrdresTravail
            .FirstOrDefaultAsync(o => o.Id == id && o.SocieteId == societeId);
        if (existing == null) return NotFound();

        // ── WORK ORDER STATE MACHINE VALIDATION ────────────────────────────
        var currentStatut = existing.Statut;
        var targetStatut = ordre.Statut;

        if (currentStatut != targetStatut)
        {
            // 1. Guard against modifications from terminal states
            if (currentStatut == StatutOT.Cloture && !User.IsInRole("SuperAdmin"))
            {
                return Conflict("Un ordre de travail clôturé ne peut plus changer de statut.");
            }
            if (currentStatut == StatutOT.Annule && !User.IsInRole("SuperAdmin"))
            {
                return Conflict("Un ordre de travail annulé ne peut plus changer de statut.");
            }

            // 2. Guard for Clôture administrative (StatutOT.Cloture = 6)
            if (targetStatut == StatutOT.Cloture)
            {
                bool canClose = User.IsInRole("SuperAdmin") ||
                                User.IsInRole("Administrateur") ||
                                User.IsInRole("CompanyAdmin") ||
                                User.IsInRole("ResponsableMaintenance") ||
                                User.HasClaim("Permission", "WORKORDER_CLOSE") ||
                                User.HasClaim("Permission", "WORKORDER_UPDATE_ALL");

                if (!canClose)
                {
                    return Forbid();
                }

                if (currentStatut != StatutOT.Termine && currentStatut != StatutOT.EnCours && !User.IsInRole("SuperAdmin"))
                {
                    return Conflict("Un ordre de travail doit être au moins en cours ou terminé avant d'être clôturé administrativement.");
                }
            }

            // 3. Guard: From Termine (5), only allowed transitions are Cloture (6) or Reopen to EnCours (3)
            if (currentStatut == StatutOT.Termine && targetStatut != StatutOT.Cloture && targetStatut != StatutOT.EnCours && !User.IsInRole("SuperAdmin"))
            {
                return Conflict("Un ordre de travail terminé ne peut être que clôturé ou réouvert pour ré-intervention.");
            }
        }

        // Patch only scalar/value properties
        if (string.IsNullOrWhiteSpace(existing.NumeroOT) &&
            !string.IsNullOrWhiteSpace(ordre.NumeroOT) && ordre.NumeroOT.Length <= 30)
        {
            existing.NumeroOT = ordre.NumeroOT;
        }
        existing.DemandeId         = ordre.DemandeId ?? existing.DemandeId;
        if (ordre.EquipementId > 0)  existing.EquipementId  = ordre.EquipementId;
        if (ordre.ResponsableId > 0) existing.ResponsableId = ordre.ResponsableId;
        existing.TechnicienId      = ordre.TechnicienId ?? existing.TechnicienId;
        existing.Priorite          = ordre.Priorite;
        existing.TypeMaintenance   = ordre.TypeMaintenance;
        existing.Statut            = targetStatut;
        existing.DateDebutPrevue   = ordre.DateDebutPrevue ?? existing.DateDebutPrevue;
        existing.DateFinPrevue     = ordre.DateFinPrevue ?? existing.DateFinPrevue;
        existing.DateDebutReelle   = ordre.DateDebutReelle ?? existing.DateDebutReelle;
        existing.DateFinReelle     = ordre.DateFinReelle ?? existing.DateFinReelle;
        if (!string.IsNullOrWhiteSpace(ordre.Description))  existing.Description  = ordre.Description;
        if (!string.IsNullOrWhiteSpace(ordre.Instructions)) existing.Instructions = ordre.Instructions;
        existing.CoutMainOeuvre    = ordre.CoutMainOeuvre ?? existing.CoutMainOeuvre;
        existing.CoutPieces        = ordre.CoutPieces ?? existing.CoutPieces;
        existing.CoutSousTraitance = ordre.CoutSousTraitance ?? existing.CoutSousTraitance;
        existing.CampagneId        = ordre.CampagneId ?? existing.CampagneId;

        // Real start / end dates follow lifecycle
        if (existing.Statut == StatutOT.EnCours && existing.DateDebutReelle == null)
            existing.DateDebutReelle = DateTime.UtcNow;
        if ((existing.Statut == StatutOT.Termine || existing.Statut == StatutOT.Cloture) && existing.DateFinReelle == null)
            existing.DateFinReelle = DateTime.UtcNow;

        // Npgsql timestamptz requires UTC DateTime
        existing.DateDebutPrevue = ToUtc(existing.DateDebutPrevue);
        existing.DateFinPrevue   = ToUtc(existing.DateFinPrevue);
        existing.DateDebutReelle = ToUtc(existing.DateDebutReelle);
        existing.DateFinReelle   = ToUtc(existing.DateFinReelle);

        await _context.SaveChangesAsync();

        // Auto-update linked equipment status according to OT lifecycle
        if (existing.EquipementId > 0)
        {
            var equipement = await _context.Equipements.FirstOrDefaultAsync(e => e.Id == existing.EquipementId && e.SocieteId == societeId);
            if (equipement != null)
            {
                if (existing.Statut == StatutOT.EnCours)
                {
                    equipement.Etat = EtatEquipement.EnMaintenance;
                    equipement.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                else if (existing.Statut == StatutOT.Termine || existing.Statut == StatutOT.Cloture)
                {
                    equipement.Etat = EtatEquipement.EnService;
                    equipement.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
        }

        // Auto-close linked incident when OT is completed or closed
        if ((existing.Statut == StatutOT.Termine || existing.Statut == StatutOT.Cloture) && existing.DemandeId.HasValue && existing.DemandeId.Value > 0)
        {
            var incident = await _context.DemandesIntervention.FirstOrDefaultAsync(d => d.Id == existing.DemandeId.Value && d.SocieteId == societeId);
            if (incident != null)
            {
                incident.Statut = StatutDemande.Cloturee;
                await _context.SaveChangesAsync();
            }
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        int societeId = GetSocieteId();
        var ordre = await _context.OrdresTravail
            .FirstOrDefaultAsync(o => o.Id == id && o.SocieteId == societeId);
        if (ordre == null) return NotFound();

        _context.OrdresTravail.Remove(ordre);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
