using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FournisseursController : ControllerBase
{
    private readonly GmaoDbContext _context;

    public FournisseursController(GmaoDbContext context)
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
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var hasTenantHeader = Request.Headers.ContainsKey("X-Tenant-Id");

        IQueryable<Fournisseur> query = _context.Fournisseurs.AsNoTracking();

        if (hasTenantHeader || !isSuperAdmin)
        {
            query = query.Where(f => f.SocieteId == societeId);
        }

        var fournisseurs = await query.OrderBy(f => f.Nom).ToListAsync();
        return Ok(fournisseurs);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var fournisseur = await _context.Fournisseurs.FindAsync(id);
        if (fournisseur == null) return NotFound();
        return Ok(fournisseur);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Fournisseur fournisseur)
    {
        if (string.IsNullOrWhiteSpace(fournisseur.Nom))
            return BadRequest(new { message = "Le nom du fournisseur est obligatoire." });

        fournisseur.SocieteId = GetSocieteId();

        // Ensure unique code per societe (and overall to avoid constraint issues)
        if (string.IsNullOrWhiteSpace(fournisseur.Code) ||
            await _context.Fournisseurs.AnyAsync(f => f.Code == fournisseur.Code))
        {
            int count = await _context.Fournisseurs.CountAsync(f => f.SocieteId == fournisseur.SocieteId);
            string candidate = $"SUP-{fournisseur.SocieteId}-{count + 1:D3}";
            while (await _context.Fournisseurs.AnyAsync(f => f.Code == candidate))
            {
                count++;
                candidate = $"SUP-{fournisseur.SocieteId}-{count + 1:D3}";
            }
            fournisseur.Code = candidate;
        }

        fournisseur.CreatedAt = DateTime.UtcNow;
        _context.Fournisseurs.Add(fournisseur);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = fournisseur.Id }, fournisseur);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Fournisseur fournisseur)
    {
        if (id != fournisseur.Id) return BadRequest();
        
        var existing = await _context.Fournisseurs.FirstOrDefaultAsync(f => f.Id == id);
        if (existing == null) return NotFound();

        existing.Nom = fournisseur.Nom;
        existing.Contact = fournisseur.Contact;
        existing.Telephone = fournisseur.Telephone;
        existing.Email = fournisseur.Email;
        existing.Adresse = fournisseur.Adresse;
        existing.IsActive = fournisseur.IsActive;

        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var fournisseur = await _context.Fournisseurs
            .Include(f => f.Equipements)
            .Include(f => f.Pieces)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (fournisseur == null) return NotFound();

        foreach (var eq in fournisseur.Equipements)
        {
            eq.FournisseurId = null;
        }
        foreach (var p in fournisseur.Pieces)
        {
            p.FournisseurId = null;
        }

        _context.Fournisseurs.Remove(fournisseur);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

