using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.API.Security;
using GMAO.Application.Interfaces;
using GMAO.Application.Security;
using GMAO.Domain.Constants;
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
public class OrdresTravailController : ControllerBase
{
    private static readonly IReadOnlyDictionary<StatutOT, StatutOT[]> AllowedTransitions =
        new Dictionary<StatutOT, StatutOT[]>
        {
            [StatutOT.Cree]        = new[] { StatutOT.Planifie, StatutOT.AffecteChef, StatutOT.EnCours, StatutOT.Suspendu, StatutOT.Annule },
            [StatutOT.AffecteChef] = new[] { StatutOT.Planifie, StatutOT.EnCours, StatutOT.Suspendu, StatutOT.Annule },
            [StatutOT.Planifie]    = new[] { StatutOT.EnCours, StatutOT.AffecteChef, StatutOT.Suspendu, StatutOT.Annule },
            [StatutOT.EnCours]     = new[] { StatutOT.Planifie, StatutOT.Suspendu, StatutOT.Termine, StatutOT.Cloture, StatutOT.Annule },
            [StatutOT.Suspendu]    = new[] { StatutOT.EnCours, StatutOT.Planifie, StatutOT.Annule },
            [StatutOT.Termine]     = new[] { StatutOT.EnCours, StatutOT.Cloture },
            [StatutOT.Cloture]     = Array.Empty<StatutOT>(),
            [StatutOT.Annule]      = Array.Empty<StatutOT>()
        };

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
    [HasPermission(GmaoPermissions.WorkOrderView)]
    public async Task<IActionResult> GetAll()
    {
        int societeId = GetSocieteId();
        int currentUserId = GetCurrentUserId();

        var query = _context.OrdresTravail
            .AsNoTracking()
            .Include(o => o.Campagne)
            .Include(o => o.Technicien)
            .Include(o => o.Intervention)
            .Include(o => o.MouvementsStock)
                .ThenInclude(m => m.Piece)
            .Where(o => o.SocieteId == societeId);

        var scope = GmaoPermissions.GetScope(User, GmaoPermissions.WorkOrderView);
        if (scope == AccessScope.None)
            return Forbid();

        if (scope == AccessScope.Team)
        {
            int? teamId = await _context.Users.AsNoTracking()
                .Where(u => u.Id == currentUserId && u.SocieteId == societeId)
                .Select(u => u.EquipeId)
                .FirstOrDefaultAsync();

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
        else if (scope == AccessScope.Own)
        {
            query = query.Where(ot => ot.TechnicienId == currentUserId || ot.ResponsableId == currentUserId);
        }

        var ordres = await query.ToListAsync();
        return Ok(ordres);
    }

    [HttpGet("{id}")]
    [HasPermission(GmaoPermissions.WorkOrderView)]
    public async Task<IActionResult> GetById(int id)
    {
        int societeId = GetSocieteId();
        int currentUserId = GetCurrentUserId();

        var ordre = await _context.OrdresTravail
            .AsNoTracking()
            .Include(o => o.Campagne)
            .Include(o => o.Technicien)
            .Include(o => o.Intervention)
            .Include(o => o.MouvementsStock)
                .ThenInclude(m => m.Piece)
            .FirstOrDefaultAsync(o => o.Id == id && o.SocieteId == societeId);

        if (ordre == null) return NotFound();

        var scope = GmaoPermissions.GetScope(User, GmaoPermissions.WorkOrderView);
        if (scope == AccessScope.None)
            return Forbid();

        if (scope == AccessScope.Team)
        {
            int? teamId = await _context.Users.AsNoTracking()
                .Where(u => u.Id == currentUserId && u.SocieteId == societeId)
                .Select(u => u.EquipeId)
                .FirstOrDefaultAsync();

            bool isUserTeam = teamId.HasValue && (
                (ordre.Technicien != null && ordre.Technicien.EquipeId == teamId.Value) ||
                (ordre.Responsable != null && ordre.Responsable.EquipeId == teamId.Value));

            if (ordre.TechnicienId != currentUserId && ordre.ResponsableId != currentUserId && !isUserTeam)
                return Forbid();
        }
        else if (scope == AccessScope.Own)
        {
            if (ordre.TechnicienId != currentUserId && ordre.ResponsableId != currentUserId)
                return Forbid();
        }

        return Ok(ordre);
    }

    /// <summary>
    /// Creates a new work order with strict tenant validation, collision-free NumeroOT generation,
    /// active campaign association, and incident status synchronization.
    /// </summary>
    [HttpPost]
    [HasPermission(GmaoPermissions.WorkOrderCreate)]
    public async Task<IActionResult> Create([FromBody] OrdresTravail ordre)
    {
        int societeId = GetSocieteId();
        ordre.SocieteId = societeId;

        if (ordre.EquipementId <= 0)
            return BadRequest("L'équipement associé à l'ordre de travail est obligatoire.");

        var equipement = await _context.Equipements
            .FirstOrDefaultAsync(e => e.Id == ordre.EquipementId && e.SocieteId == societeId);
        if (equipement == null)
            return BadRequest("L'équipement spécifié n'existe pas ou n'appartient pas à votre société.");

        if (ordre.ResponsableId <= 0)
        {
            int currentUserId = GetCurrentUserId();
            if (currentUserId > 0)
                ordre.ResponsableId = currentUserId;
        }

        if (ordre.DemandeId.HasValue && ordre.DemandeId.Value > 0)
        {
            var incident = await _context.DemandesIntervention
                .FirstOrDefaultAsync(d => d.Id == ordre.DemandeId.Value && d.SocieteId == societeId);
            if (incident == null)
                return BadRequest("La demande d'intervention référencée est introuvable dans votre société.");

            var existingForDemande = await _context.OrdresTravail
                .FirstOrDefaultAsync(o => o.DemandeId == ordre.DemandeId.Value && o.SocieteId == societeId);
            if (existingForDemande != null)
                return Ok(existingForDemande);
        }

        int currentYear = DateTime.UtcNow.Year;
        string yearPrefix = $"OT-{currentYear}-S{societeId}-";

        var existingNums = await _context.OrdresTravail
            .IgnoreQueryFilters()
            .Where(o => o.SocieteId == societeId && o.NumeroOT.StartsWith(yearPrefix))
            .Select(o => o.NumeroOT)
            .ToListAsync();

        int maxSeq = 0;
        foreach (var num in existingNums)
        {
            var parts = num.Split('-');
            if (parts.Length >= 4 && int.TryParse(parts[^1], out int s) && s > maxSeq)
                maxSeq = s;
        }

        int seq = maxSeq + 1;
        string candidate;
        do
        {
            candidate = $"{yearPrefix}{seq:D3}";
            seq++;
        } while (await _context.OrdresTravail.IgnoreQueryFilters().AnyAsync(o => o.NumeroOT == candidate));

        ordre.NumeroOT = candidate;
        ordre.DateCreation = DateTime.UtcNow;
        ordre.DateDebutPrevue = ToUtc(ordre.DateDebutPrevue);
        ordre.DateFinPrevue = ToUtc(ordre.DateFinPrevue);
        ordre.DateDebutReelle = ToUtc(ordre.DateDebutReelle);
        ordre.DateFinReelle = ToUtc(ordre.DateFinReelle);

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

        if (ordre.Intervention != null)
        {
            ordre.Intervention.DateIntervention = DateTime.UtcNow;
            ordre.Intervention.DateDebut = ToUtc(ordre.Intervention.DateDebut);
            ordre.Intervention.DateFin = ToUtc(ordre.Intervention.DateFin);
        }

        await _repository.AddAsync(ordre);

        if (ordre.DemandeId.HasValue)
        {
            var incident = await _incidentRepository.GetByIdAsync(ordre.DemandeId.Value);
            if (incident != null && incident.SocieteId == societeId)
            {
                incident.Statut = StatutDemande.TransformeeEnOT;
                await _incidentRepository.UpdateAsync(incident);
            }
        }

        if (ordre.TypeMaintenance == TypeMaintenance.Corrective || (int)ordre.Priorite >= (int)PrioriteIntervention.Haute)
        {
            equipement.Etat = EtatEquipement.EnPanne;
            equipement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetById), new { id = ordre.Id }, ordre);
    }

    /// <summary>
    /// Updates a work order, strictly enforcing RBAC scopes, valid OT state transitions,
    /// administrative closure rules, equipment status synchronization, and Intervention persistence.
    /// </summary>
    [HttpPut("{id}")]
    [HasPermission(
        GmaoPermissions.WorkOrderUpdate,
        GmaoPermissions.WorkOrderExecute,
        GmaoPermissions.WorkOrderStart,
        GmaoPermissions.WorkOrderSuspend,
        GmaoPermissions.WorkOrderFinish,
        GmaoPermissions.WorkOrderClose,
        GmaoPermissions.WorkOrderAssign)]
    public async Task<IActionResult> Update(int id, [FromBody] OrdresTravail ordre)
    {
        if (id != ordre.Id) return BadRequest("L'id dans l'URL ne correspond pas au corps.");

        int societeId = GetSocieteId();
        int currentUserId = GetCurrentUserId();

        var existing = await _context.OrdresTravail
            .Include(o => o.Intervention)
            .FirstOrDefaultAsync(o => o.Id == id && o.SocieteId == societeId);
        if (existing == null) return NotFound();

        if (!CanModifyWorkOrder(existing, currentUserId, societeId))
            return Forbid();

        if (ordre.EquipementId > 0 && ordre.EquipementId != existing.EquipementId)
        {
            var eqExists = await _context.Equipements.AsNoTracking()
                .AnyAsync(e => e.Id == ordre.EquipementId && e.SocieteId == societeId);
            if (!eqExists)
                return BadRequest("L'équipement cible n'existe pas ou n'appartient pas à votre société.");
        }

        var currentStatut = existing.Statut;
        var targetStatut = ordre.Statut;

        if (currentStatut != targetStatut)
        {
            bool isSuperAdmin = User.IsInRole(GmaoRoles.SuperAdmin);

            if (currentStatut == StatutOT.Cloture && !isSuperAdmin)
                return Conflict("Un ordre de travail clôturé ne peut plus changer de statut.");

            if (currentStatut == StatutOT.Annule && !isSuperAdmin)
                return Conflict("Un ordre de travail annulé ne peut plus changer de statut.");

            if (targetStatut == StatutOT.Cloture)
            {
                bool canClose = GmaoRoles.IsManagerial(User) ||
                                GmaoPermissions.HasPermission(User, GmaoPermissions.WorkOrderClose) ||
                                GmaoPermissions.GetScope(User, GmaoPermissions.WorkOrderUpdate) == AccessScope.All;

                if (!canClose)
                    return Forbid();

                if (currentStatut != StatutOT.Termine && currentStatut != StatutOT.EnCours && !isSuperAdmin)
                    return Conflict("Un ordre de travail doit être au moins en cours ou terminé avant d'être clôturé administrativement.");
            }

            if (targetStatut == StatutOT.Annule && !isSuperAdmin)
            {
                bool canCancel = GmaoRoles.IsManagerial(User) ||
                                 GmaoPermissions.HasPermission(User, GmaoPermissions.WorkOrderDelete) ||
                                 GmaoPermissions.HasPermission(User, GmaoPermissions.WorkOrderClose);
                if (!canCancel)
                    return Forbid();
            }

            if (!isSuperAdmin)
            {
                if (currentStatut == StatutOT.Termine && targetStatut != StatutOT.Cloture && targetStatut != StatutOT.EnCours)
                    return Conflict("Un ordre de travail terminé ne peut être que clôturé ou réouvert pour ré-intervention.");

                if (!AllowedTransitions.TryGetValue(currentStatut, out var validTargets) || !validTargets.Contains(targetStatut))
                    return Conflict($"Transition de statut invalide : {currentStatut} vers {targetStatut}.");
            }
        }

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

        if (existing.Statut == StatutOT.EnCours && existing.DateDebutReelle == null)
            existing.DateDebutReelle = DateTime.UtcNow;
        if ((existing.Statut == StatutOT.Termine || existing.Statut == StatutOT.Cloture) && existing.DateFinReelle == null)
            existing.DateFinReelle = DateTime.UtcNow;

        existing.DateDebutPrevue = ToUtc(existing.DateDebutPrevue);
        existing.DateFinPrevue   = ToUtc(existing.DateFinPrevue);
        existing.DateDebutReelle = ToUtc(existing.DateDebutReelle);
        existing.DateFinReelle   = ToUtc(existing.DateFinReelle);

        if (ordre.Intervention != null)
        {
            existing.Intervention ??= new Intervention
            {
                OTId = existing.Id,
                DateIntervention = DateTime.UtcNow
            };

            if (ordre.Intervention.Diagnostic != null)
                existing.Intervention.Diagnostic = ordre.Intervention.Diagnostic;
            if (ordre.Intervention.CausePanne != null)
                existing.Intervention.CausePanne = ordre.Intervention.CausePanne;
            if (ordre.Intervention.Solution != null)
                existing.Intervention.Solution = ordre.Intervention.Solution;
            if (ordre.Intervention.Observation != null)
                existing.Intervention.Observation = ordre.Intervention.Observation;
            if (ordre.Intervention.TempsPasse.HasValue)
                existing.Intervention.TempsPasse = ordre.Intervention.TempsPasse;

            existing.Intervention.DateDebut = ToUtc(ordre.Intervention.DateDebut) ?? existing.DateDebutReelle;
            existing.Intervention.DateFin   = ToUtc(ordre.Intervention.DateFin) ?? existing.DateFinReelle;
            existing.Intervention.Statut    = existing.Statut switch
            {
                StatutOT.EnCours  => StatutIntervention.EnCours,
                StatutOT.Suspendu => StatutIntervention.Suspendue,
                StatutOT.Termine or StatutOT.Cloture => StatutIntervention.Terminee,
                _ => existing.Intervention.Statut
            };
        }

        await _context.SaveChangesAsync();

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
                else if (existing.Statut == StatutOT.Termine || existing.Statut == StatutOT.Cloture || existing.Statut == StatutOT.Annule)
                {
                    equipement.Etat = EtatEquipement.EnService;
                    equipement.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
        }

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
    [HasPermission(GmaoPermissions.WorkOrderDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        int societeId = GetSocieteId();
        var ordre = await _context.OrdresTravail
            .FirstOrDefaultAsync(o => o.Id == id && o.SocieteId == societeId);
        if (ordre == null) return NotFound();

        if (ordre.Statut == StatutOT.Cloture && !User.IsInRole(GmaoRoles.SuperAdmin))
            return Conflict("Un ordre de travail clôturé ne peut pas être supprimé.");

        _context.OrdresTravail.Remove(ordre);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private bool CanModifyWorkOrder(OrdresTravail existing, int currentUserId, int societeId)
    {
        if (GmaoRoles.IsCompanyAdmin(User) || GmaoRoles.IsManagerial(User))
            return true;

        var updateScope = GmaoPermissions.GetScope(User, GmaoPermissions.WorkOrderUpdate);
        if (updateScope == AccessScope.All || GmaoPermissions.HasPermission(User, GmaoPermissions.WorkOrderClose))
            return true;

        if (updateScope == AccessScope.Team)
        {
            if (existing.TechnicienId == currentUserId || existing.ResponsableId == currentUserId)
                return true;

            int? teamId = _context.Users.AsNoTracking()
                .Where(u => u.Id == currentUserId && u.SocieteId == societeId)
                .Select(u => u.EquipeId)
                .FirstOrDefault();

            if (teamId.HasValue)
            {
                bool techInTeam = existing.TechnicienId.HasValue && _context.Users.AsNoTracking()
                    .Any(u => u.Id == existing.TechnicienId.Value && u.EquipeId == teamId.Value);
                bool respInTeam = existing.ResponsableId > 0 && _context.Users.AsNoTracking()
                    .Any(u => u.Id == existing.ResponsableId && u.EquipeId == teamId.Value);
                if (techInTeam || respInTeam)
                    return true;
            }

            // Un chef d'équipe peut prendre en charge / planifier un OT encore non affecté à un technicien
            if (existing.TechnicienId == null && (existing.Statut == StatutOT.Cree || existing.Statut == StatutOT.AffecteChef))
                return true;

            return false;
        }

        // Scope OWN ou permissions d'exécution technicien
        bool hasExecPerm = GmaoPermissions.HasPermission(User, GmaoPermissions.WorkOrderExecute) ||
                           GmaoPermissions.HasPermission(User, GmaoPermissions.WorkOrderStart) ||
                           GmaoPermissions.HasPermission(User, GmaoPermissions.WorkOrderSuspend) ||
                           GmaoPermissions.HasPermission(User, GmaoPermissions.WorkOrderFinish);

        if (updateScope == AccessScope.Own || hasExecPerm)
            return existing.TechnicienId == currentUserId || existing.ResponsableId == currentUserId;

        return false;
    }
}
