using GMAO.API.Security;
using GMAO.Application.Interfaces;
using GMAO.Application.Security;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FournisseursController : ControllerBase
{
    private readonly GmaoDbContext _context;
    private readonly ITenantContextService _tenantContext;

    public FournisseursController(GmaoDbContext context, ITenantContextService tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    private int GetSocieteId() => _tenantContext.GetRequiredTenantId();

    [HttpGet]
    [HasPermission(GmaoPermissions.SupplierView)]
    public async Task<IActionResult> GetAll()
    {
        var societeId = GetSocieteId();
        var fournisseurs = await _context.Fournisseurs
            .AsNoTracking()
            .Where(f => f.SocieteId == societeId)
            .OrderBy(f => f.Nom)
            .ToListAsync();
        return Ok(fournisseurs);
    }

    [HttpGet("{id}")]
    [HasPermission(GmaoPermissions.SupplierView)]
    public async Task<IActionResult> GetById(int id)
    {
        var societeId = GetSocieteId();
        var fournisseur = await _context.Fournisseurs
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.SocieteId == societeId);
        if (fournisseur == null) return NotFound();
        return Ok(fournisseur);
    }

    [HttpPost]
    [HasPermission(GmaoPermissions.SupplierCreate)]
    public async Task<IActionResult> Create([FromBody] Fournisseur fournisseur)
    {
        if (string.IsNullOrWhiteSpace(fournisseur.Nom))
            return BadRequest(new { message = "Le nom du fournisseur est obligatoire." });

        fournisseur.SocieteId = GetSocieteId();

        if (string.IsNullOrWhiteSpace(fournisseur.Code) ||
            await _context.Fournisseurs.IgnoreQueryFilters().AnyAsync(f => f.Code == fournisseur.Code))
        {
            int count = await _context.Fournisseurs.CountAsync(f => f.SocieteId == fournisseur.SocieteId);
            string candidate = $"SUP-{fournisseur.SocieteId}-{count + 1:D3}";
            while (await _context.Fournisseurs.IgnoreQueryFilters().AnyAsync(f => f.Code == candidate))
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
    [HasPermission(GmaoPermissions.SupplierUpdate)]
    public async Task<IActionResult> Update(int id, [FromBody] Fournisseur fournisseur)
    {
        if (id != fournisseur.Id) return BadRequest();

        var societeId = GetSocieteId();
        var existing = await _context.Fournisseurs.FirstOrDefaultAsync(f => f.Id == id && f.SocieteId == societeId);
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
    [HasPermission(GmaoPermissions.SupplierDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var societeId = GetSocieteId();
        var fournisseur = await _context.Fournisseurs
            .Include(f => f.Equipements)
            .Include(f => f.Pieces)
            .FirstOrDefaultAsync(f => f.Id == id && f.SocieteId == societeId);

        if (fournisseur == null) return NotFound();

        foreach (var eq in fournisseur.Equipements)
            eq.FournisseurId = null;

        foreach (var p in fournisseur.Pieces)
            p.FournisseurId = null;

        _context.Fournisseurs.Remove(fournisseur);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
