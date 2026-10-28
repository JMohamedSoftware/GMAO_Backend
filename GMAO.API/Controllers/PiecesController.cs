using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PiecesController : ControllerBase
{
    private readonly IGenericRepository<Piece> _repository;
    private readonly GmaoDbContext _context;

    public PiecesController(IGenericRepository<Piece> repository, GmaoDbContext context)
    {
        _repository = repository;
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

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        int societeId = GetSocieteId();
        var pieces = await _context.Pieces
            .Where(p => p.SocieteId == societeId || (societeId == 1 && (p.SocieteId == null || p.SocieteId == 0)))
            .OrderBy(p => p.Reference)
            .ToListAsync();
        return Ok(pieces);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        int societeId = GetSocieteId();
        var piece = await _context.Pieces
            .FirstOrDefaultAsync(p => p.Id == id && (p.SocieteId == societeId || (societeId == 1 && (p.SocieteId == null || p.SocieteId == 0))));
        if (piece == null) return NotFound();
        return Ok(piece);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Piece piece)
    {
        int societeId = GetSocieteId();
        piece.SocieteId = societeId;

        // Auto-generate or resolve reference if empty or already in use for this tenant
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
    public async Task<IActionResult> Update(int id, [FromBody] Piece piece)
    {
        if (id != piece.Id) return BadRequest();

        int societeId = GetSocieteId();
        var existing = await _context.Pieces
            .FirstOrDefaultAsync(p => p.Id == id && p.SocieteId == societeId);
        if (existing == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(piece.Reference))
        {
            // Verify reference uniqueness within company (excluding this piece)
            var refExists = await _context.Pieces.AnyAsync(p => p.Id != id && p.SocieteId == societeId && p.Reference.ToLower() == piece.Reference.Trim().ToLower());
            if (!refExists)
            {
                existing.Reference = piece.Reference.Trim();
            }
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
