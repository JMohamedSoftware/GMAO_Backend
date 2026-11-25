using System;
using System.Linq;
using System.Threading.Tasks;
using GMAO.API.Security;
using GMAO.Application.Interfaces;
using GMAO.Application.Security;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PiecesController : ControllerBase
{
    private readonly IGenericRepository<Piece> _repository;
    private readonly GmaoDbContext _context;
    private readonly ITenantContextService _tenantContext;

    public PiecesController(
        IGenericRepository<Piece> repository,
        GmaoDbContext context,
        ITenantContextService tenantContext)
    {
        _repository = repository;
        _context = context;
        _tenantContext = tenantContext;
    }

    private int GetSocieteId() => _tenantContext.GetRequiredTenantId();

    [HttpGet]
    [HasPermission(GmaoPermissions.InventoryView)]
    public async Task<IActionResult> GetAll()
    {
        int societeId = GetSocieteId();
        var pieces = await _context.Pieces
            .AsNoTracking()
            .Where(p => p.SocieteId == societeId)
            .OrderBy(p => p.Reference)
            .ToListAsync();
        return Ok(pieces);
    }

    [HttpGet("{id}")]
    [HasPermission(GmaoPermissions.InventoryView)]
    public async Task<IActionResult> GetById(int id)
    {
        int societeId = GetSocieteId();
        var piece = await _context.Pieces
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.SocieteId == societeId);
        if (piece == null) return NotFound();
        return Ok(piece);
    }

    [HttpPost]
    [HasPermission(GmaoPermissions.InventoryCreate)]
    public async Task<IActionResult> Create([FromBody] Piece piece)
    {
        if (piece.StockActuel < 0 || piece.StockMinimum < 0 || piece.PrixUnitaire < 0)
            return BadRequest(new { message = "Les quantités de stock et prix unitaire ne peuvent pas être négatifs." });

        int societeId = GetSocieteId();
        piece.SocieteId = societeId;

        if (piece.FournisseurId.HasValue && piece.FournisseurId.Value > 0)
        {
            var supValid = await _context.Fournisseurs.AsNoTracking()
                .AnyAsync(f => f.Id == piece.FournisseurId.Value && f.SocieteId == societeId);
            if (!supValid)
                return BadRequest(new { message = "Le fournisseur sélectionné n'appartient pas à votre société." });
        }

        if (string.IsNullOrWhiteSpace(piece.Reference))
        {
            var count = await _context.Pieces.CountAsync(p => p.SocieteId == societeId);
            piece.Reference = $"REF-{societeId}-{(count + 1):D3}";
        }
        else
        {
            var exists = await _context.Pieces.AnyAsync(p => p.SocieteId == societeId && p.Reference.ToLower() == piece.Reference.Trim().ToLower());
            if (exists)
            {
                var count = await _context.Pieces.CountAsync(p => p.SocieteId == societeId);
                piece.Reference = $"{piece.Reference.Trim()}-{(count + 1)}";
            }
        }

        piece.CreatedAt = DateTime.UtcNow;
        piece.UpdatedAt = DateTime.UtcNow;

        _context.Pieces.Add(piece);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = piece.Id }, piece);
    }

    [HttpPut("{id}")]
    [HasPermission(GmaoPermissions.InventoryUpdate)]
    public async Task<IActionResult> Update(int id, [FromBody] Piece piece)
    {
        if (id != piece.Id) return BadRequest();
        if (piece.StockActuel < 0 || piece.StockMinimum < 0 || piece.PrixUnitaire < 0)
            return BadRequest(new { message = "Les quantités de stock et prix unitaire ne peuvent pas être négatifs." });

        int societeId = GetSocieteId();
        var existing = await _context.Pieces
            .FirstOrDefaultAsync(p => p.Id == id && p.SocieteId == societeId);
        if (existing == null) return NotFound();

        if (piece.FournisseurId.HasValue && piece.FournisseurId.Value > 0)
        {
            var supValid = await _context.Fournisseurs.AsNoTracking()
                .AnyAsync(f => f.Id == piece.FournisseurId.Value && f.SocieteId == societeId);
            if (!supValid)
                return BadRequest(new { message = "Le fournisseur sélectionné n'appartient pas à votre société." });
        }

        if (!string.IsNullOrWhiteSpace(piece.Reference))
        {
            var refExists = await _context.Pieces.AnyAsync(p => p.Id != id && p.SocieteId == societeId && p.Reference.ToLower() == piece.Reference.Trim().ToLower());
            if (!refExists)
                existing.Reference = piece.Reference.Trim();
        }

        existing.Designation = piece.Designation ?? existing.Designation;
        existing.FamillePieceId = piece.FamillePieceId ?? existing.FamillePieceId;
        existing.FournisseurId = piece.FournisseurId ?? existing.FournisseurId;
        existing.StockActuel = piece.StockActuel;
        existing.StockMinimum = piece.StockMinimum;
        existing.StockMaximum = piece.StockMaximum;
        existing.PrixUnitaire = piece.PrixUnitaire;
        existing.Emplacement = piece.Emplacement ?? existing.Emplacement;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [HasPermission(GmaoPermissions.InventoryDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        int societeId = GetSocieteId();
        var piece = await _context.Pieces
            .FirstOrDefaultAsync(p => p.Id == id && p.SocieteId == societeId);
        if (piece == null) return NotFound();

        _context.Pieces.Remove(piece);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
