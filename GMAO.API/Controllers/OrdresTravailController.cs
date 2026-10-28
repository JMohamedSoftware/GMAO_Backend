using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Domain.Interfaces;
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

    public OrdresTravailController(
        IGenericRepository<OrdresTravail> repository,
        IGenericRepository<DemandeIntervention> incidentRepository,
        GmaoDbContext context)
    {
        _repository = repository;
        _incidentRepository = incidentRepository;
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
        var ordres = await _context.OrdresTravail
            .Where(o => o.SocieteId == societeId)
            .ToListAsync();
        return Ok(ordres);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        int societeId = GetSocieteId();
        var ordre = await _context.OrdresTravail
            .FirstOrDefaultAsync(o => o.Id == id && o.SocieteId == societeId);
        if (ordre == null) return NotFound();
        return Ok(ordre);
    }

    /// <summary>
    /// Creates a new work order. Auto-generates a clean sequential NumeroOT
    /// (e.g. OT-2026-001) scoped strictly to the authenticated company.
    /// If demandeId is provided, also sets the corresponding incident status to TransformeeEnOT (4).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrdresTravail ordre)
    {
        int societeId = GetSocieteId();
        ordre.SocieteId = societeId;

        // Auto-generate clean sequential NumeroOT per company and per year (OT-2026-001, OT-2026-002...)
        int currentYear = DateTime.UtcNow.Year;
        string yearPrefix = $"OT-{currentYear}-";

        var existingOts = await _context.OrdresTravail
            .Where(o => o.SocieteId == societeId)
            .ToListAsync();
        int maxSeq = 0;
        foreach (var o in existingOts)
        {
            if (!string.IsNullOrEmpty(o.NumeroOT) && o.NumeroOT.StartsWith(yearPrefix))
            {
                var numPart = o.NumeroOT.Substring(yearPrefix.Length);
                if (int.TryParse(numPart, out int s) && s > maxSeq)
                {
                    maxSeq = s;
                }
            }
        }

        ordre.NumeroOT = $"{yearPrefix}{(maxSeq + 1):D3}";

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
    /// Updates a work order. Copies scalar fields onto the tracked entity to avoid
    /// EF Core "duplicate tracking" InvalidOperationException.
    /// Also updates the related equipment status according to the OT lifecycle:
    /// - En cours (3) -> Equipement En maintenance (3)
    /// - Terminé (5) or Clôturé (6) -> Equipement En service (1)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] OrdresTravail ordre)
    {
        if (id != ordre.Id) return BadRequest("L'id dans l'URL ne correspond pas au corps.");

        int societeId = GetSocieteId();
        var existing = await _context.OrdresTravail
            .FirstOrDefaultAsync(o => o.Id == id && o.SocieteId == societeId);
        if (existing == null) return NotFound();

        // Patch only scalar/value properties.
        // NumeroOT is an immutable identifier (unique index, max 30 chars) — never
        // overwrite it with free text (the frontend used to send the title here,
        // which broke the unique index for preventive OTs sharing the same description).
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
        existing.Statut            = ordre.Statut;
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

        // Real start / end dates follow the lifecycle
        if (existing.Statut == StatutOT.EnCours && existing.DateDebutReelle == null)
            existing.DateDebutReelle = DateTime.UtcNow;
        if ((existing.Statut == StatutOT.Termine || (int)existing.Statut == 6) && existing.DateFinReelle == null)
            existing.DateFinReelle = DateTime.UtcNow;

        // Npgsql (timestamptz) refuses non-UTC DateTime values
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
                else if (existing.Statut == StatutOT.Termine || (int)existing.Statut == 6) // Terminé or Clôturé
                {
                    equipement.Etat = EtatEquipement.EnService;
                    equipement.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
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
