using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using GMAO.Infrastructure.Data;
using System.Security.Claims;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlansPreventifController : ControllerBase
{
    private readonly GmaoDbContext _context;
    private readonly IGenericRepository<OrdresTravail> _otRepository;

    public PlansPreventifController(GmaoDbContext context, IGenericRepository<OrdresTravail> otRepository)
    {
        _context = context;
        _otRepository = otRepository;
    }

    // ── GET /api/PlansPreventif ───────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var plans = await _context.PlansPreventifs
            .Where(p => p.Actif)
            .Include(p => p.Equipement)
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
    public async Task<IActionResult> GetById(int id)
    {
        var plan = await _context.PlansPreventifs
            .Where(p => p.Id == id)
            .Include(p => p.Equipement)
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
    public async Task<IActionResult> Create([FromBody] CreatePlanRequest req)
    {
        var plan = new PlanPreventif
        {
            EquipementId      = req.EquipementId,
            Titre             = req.Titre,
            Description       = req.Description,
            TypeDeclenchement = (TypeDeclenchement)req.TypeDeclenchement,
            Frequence         = req.Frequence,
            UniteMesure       = req.UniteMesure,
            DerniereDate      = req.DerniereDate,
            ProchaineDate     = req.ProchaineDate,
            Actif             = true,
            Taches            = req.Taches?.Select((t, i) => new TachePreventive
            {
                Description          = t.Description,
                Ordre                = i + 1,
                DureeEstimeeMinutes  = t.DureeEstimeeMinutes,
                EstObligatoire       = t.EstObligatoire
            }).ToList() ?? new List<TachePreventive>()
        };

        _context.PlansPreventifs.Add(plan);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = plan.Id }, new { plan.Id });
    }

    // ── PUT /api/PlansPreventif/{id} ──────────────────────────────────────────
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePlanRequest req)
    {
        var existing = await _context.PlansPreventifs
            .Include(p => p.Taches)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (existing == null) return NotFound();

        existing.EquipementId      = req.EquipementId;
        existing.Titre             = req.Titre;
        existing.Description       = req.Description;
        existing.TypeDeclenchement = (TypeDeclenchement)req.TypeDeclenchement;
        existing.Frequence         = req.Frequence;
        existing.UniteMesure       = req.UniteMesure;
        existing.DerniereDate      = req.DerniereDate;
        existing.ProchaineDate     = req.ProchaineDate;

        // Replace taches
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
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await _context.PlansPreventifs.FindAsync(id);
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
    public async Task<IActionResult> GenererOT(int id)
    {
        var plan = await _context.PlansPreventifs
            .Include(p => p.Equipement)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (plan == null) return NotFound();

        // Calcul de la prochaine date selon la fréquence
        var now = DateTime.UtcNow;
        var prochaine = plan.UniteMesure?.ToLower() switch
        {
            "jours"  => now.AddDays(plan.Frequence),
            "semaines" => now.AddDays(plan.Frequence * 7),
            "mois"   => now.AddMonths(plan.Frequence),
            _        => now.AddDays(plan.Frequence)
        };

        // Récupérer l'ID du responsable depuis le JWT
        var responsableIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int responsableId = int.TryParse(responsableIdStr, out var rid) ? rid : 1;

        var ot = new OrdresTravail
        {
            NumeroOT        = $"OT-PRV-{now.Year}-{plan.Id}-{now.Ticks % 10000}",
            EquipementId    = plan.EquipementId,
            ResponsableId   = responsableId,
            TypeMaintenance = TypeMaintenance.Preventive,
            Priorite        = PrioriteIntervention.Normale,
            Statut          = StatutOT.Planifie,
            DateCreation    = now,
            DateDebutPrevue = plan.ProchaineDate ?? now,
            Description     = $"OT Préventif généré depuis le plan: {plan.Titre}. " +
                              $"Fréquence: tous les {plan.Frequence} {plan.UniteMesure}.",
            Instructions    = $"Plan préventif {plan.Id} — Équipement: {plan.Equipement?.Designation}"
        };

        await _otRepository.AddAsync(ot);

        // Mise à jour du plan
        plan.DerniereDate  = now;
        plan.ProchaineDate = prochaine;
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
    public async Task<IActionResult> Replanifier(int id, [FromBody] ReplanifierRequest req)
    {
        var plan = await _context.PlansPreventifs.FindAsync(id);
        if (plan == null) return NotFound();

        plan.ProchaineDate = req.NouvelleDate;
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
    public int TypeDeclenchement { get; set; }    // 1=Periodique, 2=Compteur, 3=Saisonnier
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
