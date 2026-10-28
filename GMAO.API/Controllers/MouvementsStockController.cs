using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using GMAO.Infrastructure.Data;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MouvementsStockController : ControllerBase
{
    private readonly IGenericRepository<MouvementStock> _repository;

    public MouvementsStockController(IGenericRepository<MouvementStock> repository)
    {
        _repository = repository;
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
    public async Task<IActionResult> GetAll([FromServices] GmaoDbContext context)
    {
        int societeId = GetSocieteId();
        var mouvements = await context.MouvementsStock
            .Include(m => m.Piece)
            .Where(m => m.Piece != null && m.Piece.SocieteId == societeId)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
        return Ok(mouvements);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MouvementStock mouvement, [FromServices] GmaoDbContext context)
    {
        mouvement.Date = DateTime.UtcNow;
        await _repository.AddAsync(mouvement);

        // Auto-update Piece StockActuel in database
        if (mouvement.PieceId > 0)
        {
            var piece = await context.Pieces.FindAsync(mouvement.PieceId);
            if (piece != null)
            {
                if (mouvement.Type == TypeMouvementStock.Entree || (int)mouvement.Type == 1 || (int)mouvement.Type == 3)
                {
                    piece.StockActuel += mouvement.Quantite;
                }
                else if (mouvement.Type == TypeMouvementStock.Sortie || (int)mouvement.Type == 2 || (int)mouvement.Type == 4)
                {
                    piece.StockActuel = Math.Max(0, piece.StockActuel - mouvement.Quantite);
                }
                piece.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }
        }

        return CreatedAtAction(nameof(GetAll), new { id = mouvement.Id }, mouvement);
    }
}
