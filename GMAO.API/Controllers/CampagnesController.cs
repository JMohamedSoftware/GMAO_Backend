using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CampagnesController : ControllerBase
{
    private readonly GmaoDbContext _context;
    private readonly ITenantContextService _tenantContext;

    public CampagnesController(GmaoDbContext context, ITenantContextService tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    private int GetSocieteId() => _tenantContext.GetRequiredTenantId();

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var societeId = GetSocieteId();
        var campagnes = await _context.Campagnes
            .AsNoTracking()
            .Where(c => c.SocieteId == societeId)
            .OrderByDescending(c => c.DateDebut)
            .ToListAsync();

        // Si aucune campagne n'existe encore pour cette société (ex: entreprise créée avant ce patch),
        // créer automatiquement la campagne initiale en cours sans date de fin fixée
        if (campagnes.Count == 0 && societeId > 0)
        {
            var currentYear = DateTime.UtcNow.Year;
            var initialCampagne = new Campagne
            {
                SocieteId = societeId,
                Nom = $"Campagne {currentYear}",
                Description = $"Campagne industrielle principale {currentYear}",
                DateDebut = DateOnly.FromDateTime(DateTime.UtcNow),
                DateFin = null, // Date de fin ouverte tant que la campagne est en cours
                Etat = EtatCampagne.EnCours,
                CreatedAt = DateTime.UtcNow
            };
            _context.Campagnes.Add(initialCampagne);
            await _context.SaveChangesAsync();
            campagnes.Add(initialCampagne);
        }

        return Ok(campagnes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var societeId = GetSocieteId();
        var campagne = await _context.Campagnes.FirstOrDefaultAsync(c => c.Id == id && c.SocieteId == societeId);
        if (campagne == null) return NotFound();
        return Ok(campagne);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Campagne campagne)
    {
        var societeId = GetSocieteId();
        if (campagne.SocieteId <= 0)
        {
            campagne.SocieteId = societeId;
        }
        _context.Campagnes.Add(campagne);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = campagne.Id }, campagne);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Campagne campagne)
    {
        var societeId = GetSocieteId();
        var existing = await _context.Campagnes.FirstOrDefaultAsync(c => c.Id == id && c.SocieteId == societeId);
        if (existing == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(campagne.Nom))
            existing.Nom = campagne.Nom;
        if (campagne.Description != null)
            existing.Description = campagne.Description;
        if (campagne.DateDebut != default)
            existing.DateDebut = campagne.DateDebut;
        if (campagne.DateFin != null)
            existing.DateFin = campagne.DateFin;
        if (campagne.Etat != default)
            existing.Etat = campagne.Etat;

        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPost("{id}/cloturer")]
    public async Task<IActionResult> Cloturer(int id)
    {
        var societeId = GetSocieteId();
        var existing = await _context.Campagnes.FirstOrDefaultAsync(c => c.Id == id && c.SocieteId == societeId);
        if (existing == null) return NotFound();

        existing.Etat = EtatCampagne.Terminee;
        existing.DateFin = DateOnly.FromDateTime(DateTime.UtcNow);
        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var societeId = GetSocieteId();
        var campagne = await _context.Campagnes.FirstOrDefaultAsync(c => c.Id == id && c.SocieteId == societeId);
        if (campagne == null) return NotFound();
        
        _context.Campagnes.Remove(campagne);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
