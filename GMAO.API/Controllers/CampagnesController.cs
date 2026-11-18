using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Infrastructure.Data;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CampagnesController : ControllerBase
{
    private readonly GmaoDbContext _context;

    public CampagnesController(GmaoDbContext context)
    {
        _context = context;
    }

    private int GetSocieteId()
    {
        var tenantHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (int.TryParse(tenantHeader, out int tenantId) && tenantId > 0)
        {
            return tenantId;
        }

        var societeIdClaim = User.FindFirst("SocieteId")?.Value;
        if (int.TryParse(societeIdClaim, out int societeId) && societeId > 0)
            return societeId;

        return 1;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var societeId = GetSocieteId();
        var campagnes = await _context.Campagnes
            .Where(c => c.SocieteId == societeId)
            .OrderByDescending(c => c.DateDebut)
            .ToListAsync();
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
        if (campagne.DateFin != default)
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
