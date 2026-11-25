using System.Security.Claims;
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
public class PlansPreventifController : ControllerBase
{
    private readonly GmaoDbContext _context;
    private readonly IGenericRepository<OrdresTravail> _otRepository;
    private readonly ITenantContextService _tenantContext;

    public PlansPreventifController(
        GmaoDbContext context,
        IGenericRepository<OrdresTravail> otRepository,
        ITenantContextService tenantContext)
    {
        _context = context;
        _otRepository = otRepository;
        _tenantContext = tenantContext;
    }

    private int? ResolveScopedTenantId()
    {
        if (_tenantContext.IsSuperAdmin && !_tenantContext.IsImpersonating)
            return null;

        return _tenantContext.GetRequiredTenantId();
    }

    private IQueryable<PlanPreventif> ScopedPlansQuery()
    {
        var scopedTenantId = ResolveScopedTenantId();
        var query = _context.PlansPreventifs.AsQueryable();
        if (scopedTenantId.HasValue)
        {
            query = query.Where(p => p.Equipement != null && p.Equipement.SocieteId == scopedTenantId.Value);
        }
        return query;
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

    // ── GET /api/PlansPreventif ───────────────────────────────────────────────
    [HttpGet]
    [HasPermission(GmaoPermissions.PreventiveView)]
    public async Task<IActionResult> GetAll()
    {
        var plans = await ScopedPlansQuery()
            .AsNoTracking()
            .Where(p => p.Actif)
            .Include(p => p.Equipement)
            .ThenInclude(e => e.Famille)
            .Include(p => p.Taches)
            .OrderBy(p => p.ProchaineDate)
            .Select(p => new
            {
                p.Id,
                p.Titre,
                p.Description,
                p.TypeDeclenchement,
                p.Frequence,
                p.UniteMesure,
                p.DerniereDate,
                p.ProchaineDate,
                p.Actif,
                p.EquipementId,
                EquipementNom = p.Equipement.Designation,
                EquipementFamille = p.Equipement.Famille != null ? p.Equipement.Famille.Nom : null,
                Taches = p.Taches.OrderBy(t => t.Ordre).Select(t => new
                {
                    t.Id,
                    t.Description,
                    t.Ordre,
                    t.DureeEstimeeMinutes,
                    t.EstObligatoire
                })
            })
            .ToListAsync();

        return Ok(plans);
    }

    // ── GET /api/PlansPreventif/{id} ──────────────────────────────────────────
    [HttpGet("{id}")]
    [HasPermission(GmaoPermissions.PreventiveView)]
    public async Task<IActionResult> GetById(int id)
    {
        var plan = await ScopedPlansQuery()
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Include(p => p.Equipement)
            .ThenInclude(e => e.Famille)
            .Include(p => p.Taches)
            .Select(p => new
            {
                p.Id,
                p.Titre,
                p.Description,
                p.TypeDeclenchement,
                p.Frequence,
                p.UniteMesure,
                p.DerniereDate,
                p.ProchaineDate,
                p.Actif,
                p.EquipementId,
                EquipementNom = p.Equipement.Designation,
                EquipementFamille = p.Equipement.Famille != null ? p.Equipement.Famille.Nom : null,
                Taches = p.Taches.OrderBy(t => t.Ordre).Select(t => new
                {
                    t.Id,
                    t.Description,
                    t.Ordre,
                    t.DureeEstimeeMinutes,
                    t.EstObligatoire
                })
            })
            .FirstOrDefaultAsync();

        if (plan == null) return NotFound();
        return Ok(plan);
    }

    // ── POST /api/PlansPreventif ──────────────────────────────────────────────
    [HttpPost]
    [HasPermission(GmaoPermissions.PreventiveCreate)]
    public async Task<IActionResult> Create([FromBody] CreatePlanRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Titre))
            return BadRequest(new { message = "Le titre du plan préventif est obligatoire." });

        var scopedTenantId = ResolveScopedTenantId();
        var equipement = await _context.Equipements
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == req.EquipementId);

        if (equipement == null || (scopedTenantId.HasValue && equipement.SocieteId != scopedTenantId.Value))
            return BadRequest(new { message = "L'équipement spécifié n'existe pas ou n'appartient pas à votre société." });

        var plan = new PlanPreventif
        {
            EquipementId      = req.EquipementId,
            Titre             = req.Titre.Trim(),
            Description       = req.Description,
            TypeDeclenchement = (TypeDeclenchement)req.TypeDeclenchement,
            Frequence         = req.Frequence,
            UniteMesure       = req.UniteMesure,
            DerniereDate      = ToUtc(req.DerniereDate),
            ProchaineDate     = ToUtc(req.ProchaineDate),
            Actif             = true,
            Taches            = req.Taches?.Select((t, i) => new TachePreventive
            {
                Description         = t.Description,
                Ordre               = i + 1,
                DureeEstimeeMinutes = t.DureeEstimeeMinutes,
                EstObligatoire      = t.EstObligatoire
            }).ToList() ?? new List<TachePreventive>()
        };

        _context.PlansPreventifs.Add(plan);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = plan.Id }, new { plan.Id });
    }

    // ── PUT /api/PlansPreventif/{id} ──────────────────────────────────────────
    [HttpPut("{id}")]
    [HasPermission(GmaoPermissions.PreventiveUpdate)]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePlanRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Titre))
            return BadRequest(new { message = "Le titre du plan préventif est obligatoire." });

        var scopedTenantId = ResolveScopedTenantId();
        var existing = await ScopedPlansQuery()
            .Include(p => p.Equipement)
            .Include(p => p.Taches)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (existing == null) return NotFound();

        var expectedTenantId = scopedTenantId ?? existing.Equipement.SocieteId;
        var targetEq = await _context.Equipements
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == req.EquipementId && e.SocieteId == expectedTenantId);

        if (targetEq == null)
            return BadRequest(new { message = "L'équipement cible n'existe pas ou n'appartient pas à la même société." });

        existing.EquipementId      = req.EquipementId;
        existing.Titre             = req.Titre.Trim();
        existing.Description       = req.Description;
        existing.TypeDeclenchement = (TypeDeclenchement)req.TypeDeclenchement;
        existing.Frequence         = req.Frequence;
        existing.UniteMesure       = req.UniteMesure;
        existing.DerniereDate      = ToUtc(req.DerniereDate);
        existing.ProchaineDate     = ToUtc(req.ProchaineDate);

        _context.TachesPreventives.RemoveRange(existing.Taches);
        existing.Taches = req.Taches?.Select((t, i) => new TachePreventive
        {
            Description         = t.Description,
            Ordre               = i + 1,
            DureeEstimeeMinutes = t.DureeEstimeeMinutes,
            EstObligatoire      = t.EstObligatoire
        }).ToList() ?? new List<TachePreventive>();

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── DELETE /api/PlansPreventif/{id} (soft delete) ────────────────────────
    [HttpDelete("{id}")]
    [HasPermission(GmaoPermissions.PreventiveDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await ScopedPlansQuery().FirstOrDefaultAsync(p => p.Id == id);
        if (plan == null) return NotFound();

        plan.Actif = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── POST /api/PlansPreventif/{id}/generer-ot ──────────────────────────────
    /// <summary>
    /// Génère un OT Préventif à partir d'un plan et reporte sa prochaine date.
    /// </summary>
    [HttpPost("{id}/generer-ot")]
    [HasPermission(GmaoPermissions.PreventiveExecute)]
    public async Task<IActionResult> GenererOT(int id)
    {
        var scopedTenantId = ResolveScopedTenantId();
        var plan = await ScopedPlansQuery()
            .Include(p => p.Equipement)
            .Include(p => p.Taches)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plan == null) return NotFound();
        if (!plan.Actif)
            return BadRequest(new { message = "Impossible de générer un OT à partir d'un plan préventif inactif." });
        if (plan.Equipement == null)
            return BadRequest(new { message = "L'équipement associé à ce plan est introuvable." });

        int targetSocieteId = scopedTenantId ?? plan.Equipement.SocieteId;
        if (plan.Equipement.SocieteId != targetSocieteId)
            return Forbid();

        var now = DateTime.UtcNow;
        bool isSaisonnier = plan.TypeDeclenchement == TypeDeclenchement.Saisonnier
            || (plan.UniteMesure != null && (plan.UniteMesure.ToLower() == "saisonnier" || plan.UniteMesure.ToLower() == "hiver" || plan.UniteMesure.ToLower() == "printemps" || plan.UniteMesure.ToLower() == "été" || plan.UniteMesure.ToLower() == "ete" || plan.UniteMesure.ToLower() == "automne"));

        DateTime prochaine;
        if (isSaisonnier)
        {
            prochaine = now.AddMonths(plan.Frequence > 0 ? plan.Frequence * 3 : 3);
        }
        else
        {
            prochaine = plan.UniteMesure?.ToLower() switch
            {
                "jours"    => now.AddDays(plan.Frequence > 0 ? plan.Frequence : 1),
                "semaines" => now.AddDays(plan.Frequence > 0 ? plan.Frequence * 7 : 7),
                "mois"     => now.AddMonths(plan.Frequence > 0 ? plan.Frequence : 1),
                "heures"   => now.AddDays(Math.Max(1, (int)Math.Ceiling((plan.Frequence > 0 ? plan.Frequence : 24) / 24.0))),
                _          => now.AddDays(plan.Frequence > 0 ? plan.Frequence : 30)
            };
        }
        DateTime prochaineUtc = ToUtc(prochaine) ?? DateTime.UtcNow.AddDays(30);

        // Résoudre un responsable valide sans jamais supposer Id == 1
        int? responsableId = null;
        bool isSuperAdmin = User.IsInRole(GmaoRoles.SuperAdmin);
        var responsableIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(responsableIdStr, out var claimUserId) && claimUserId > 0)
        {
            var claimUserExists = await _context.Users.AsNoTracking()
                .AnyAsync(u => u.Id == claimUserId && (u.SocieteId == targetSocieteId || isSuperAdmin));
            if (claimUserExists)
                responsableId = claimUserId;
        }

        if (!responsableId.HasValue)
        {
            responsableId = await _context.Users.AsNoTracking()
                .Where(u => u.SocieteId == targetSocieteId && u.IsActive)
                .OrderBy(u => u.RoleId)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();
        }

        if (!responsableId.HasValue)
            return BadRequest(new { message = "Aucun utilisateur responsable valide n'a pu être déterminé pour cet ordre de travail." });

        var activeCampagne = await _context.Campagnes.AsNoTracking()
            .Where(c => c.SocieteId == targetSocieteId && c.Etat == EtatCampagne.EnCours)
            .OrderByDescending(c => c.DateDebut)
            .FirstOrDefaultAsync();

        string descType = plan.TypeDeclenchement == TypeDeclenchement.Saisonnier || plan.UniteMesure?.ToLower() == "saisonnier"
            ? "Intervention saisonnière"
            : $"Fréquence: tous les {plan.Frequence} {plan.UniteMesure}";

        int currentYear = now.Year;
        string yearPrefix = $"OT-{currentYear}-S{targetSocieteId}-";
        var existingNums = await _context.OrdresTravail
            .IgnoreQueryFilters()
            .Where(o => o.SocieteId == targetSocieteId && o.NumeroOT.StartsWith(yearPrefix))
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

        var ot = new OrdresTravail
        {
            NumeroOT        = candidate,
            SocieteId       = targetSocieteId,
            EquipementId    = plan.EquipementId,
            ResponsableId   = responsableId.Value,
            CampagneId      = activeCampagne?.Id,
            TypeMaintenance = TypeMaintenance.Preventive,
            Priorite        = PrioriteIntervention.Normale,
            Statut          = StatutOT.Planifie,
            DateCreation    = now,
            DateDebutPrevue = now,
            DateFinPrevue   = now.AddDays(7),
            Description     = $"OT Préventif généré depuis le plan: {plan.Titre}. {descType}.",
            Instructions    = plan.Taches != null && plan.Taches.Any()
                ? $"Plan préventif {plan.Id}\n\nTâches à réaliser:\n" + string.Join("\n", plan.Taches.OrderBy(t => t.Ordre).Select(t => $"• {t.Description}" + (t.DureeEstimeeMinutes.HasValue ? $" ({t.DureeEstimeeMinutes} min)" : "")))
                : $"Plan préventif {plan.Id} — Équipement: {plan.Equipement.Designation}"
        };

        await _otRepository.AddAsync(ot);

        plan.DerniereDate  = now;
        plan.ProchaineDate = prochaineUtc;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            otId          = ot.Id,
            numeroOT      = ot.NumeroOT,
            prochaineDate = plan.ProchaineDate
        });
    }

    // ── PUT /api/PlansPreventif/{id}/replanifier ──────────────────────────────
    [HttpPut("{id}/replanifier")]
    [HasPermission(GmaoPermissions.PreventiveUpdate)]
    public async Task<IActionResult> Replanifier(int id, [FromBody] ReplanifierRequest req)
    {
        if (req.NouvelleDate == default)
            return BadRequest(new { message = "La nouvelle date est invalide." });

        var plan = await ScopedPlansQuery().FirstOrDefaultAsync(p => p.Id == id && p.Actif);
        if (plan == null) return NotFound();

        plan.ProchaineDate = ToUtc(req.NouvelleDate);
        await _context.SaveChangesAsync();

        return Ok(new { plan.Id, plan.ProchaineDate });
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────
public class CreatePlanRequest
{
    public int EquipementId { get; set; }
    public string Titre { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TypeDeclenchement { get; set; }
    public int Frequence { get; set; }
    public string? UniteMesure { get; set; }
    public DateTime? DerniereDate { get; set; }
    public DateTime? ProchaineDate { get; set; }
    public List<TacheRequest>? Taches { get; set; }
}

public class TacheRequest
{
    public string Description { get; set; } = string.Empty;
    public int? DureeEstimeeMinutes { get; set; }
    public bool EstObligatoire { get; set; } = true;
}

public class ReplanifierRequest
{
    public DateTime NouvelleDate { get; set; }
}
