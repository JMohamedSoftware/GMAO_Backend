using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.API.Security;
using GMAO.Application.Interfaces;
using GMAO.Application.Security;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MouvementsStockController : ControllerBase
{
    private readonly IGenericRepository<MouvementStock> _repository;
    private readonly ITenantContextService _tenantContext;

    public MouvementsStockController(
        IGenericRepository<MouvementStock> repository,
        ITenantContextService tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    private int GetSocieteId() => _tenantContext.GetRequiredTenantId();

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("id")?.Value
            ?? User.FindFirst("sub")?.Value;
        return int.TryParse(claim, out int uid) ? uid : 0;
    }

    [HttpGet]
    [HasPermission(GmaoPermissions.InventoryView)]
    public async Task<IActionResult> GetAll([FromServices] GmaoDbContext context)
    {
        int societeId = GetSocieteId();
        var mouvements = await context.MouvementsStock
            .AsNoTracking()
            .Include(m => m.Piece)
            .Where(m => m.Piece != null && m.Piece.SocieteId == societeId)
            .OrderByDescending(m => m.Date)
            .Take(200)
            .ToListAsync();
        return Ok(mouvements);
    }

    [HttpPost]
    [HasPermission(GmaoPermissions.InventoryMove, GmaoPermissions.InventoryUpdate)]
    public async Task<IActionResult> Create([FromBody] MouvementStock mouvement, [FromServices] GmaoDbContext context)
    {
        if (mouvement == null)
            return BadRequest(new { message = "Le corps du mouvement de stock est obligatoire." });

        if (mouvement.PieceId <= 0)
            return BadRequest(new { message = "La pièce est obligatoire." });

        if (!Enum.IsDefined(typeof(TypeMouvementStock), mouvement.Type))
            return BadRequest(new { message = "Type de mouvement de stock invalide." });

        if (mouvement.Type == TypeMouvementStock.Inventaire)
        {
            if (mouvement.Quantite < 0)
                return BadRequest(new { message = "La quantité d'inventaire ne peut pas être négative." });
        }
        else if (mouvement.Quantite <= 0)
        {
            return BadRequest(new { message = "La quantité du mouvement doit être strictement positive." });
        }

        int societeId = GetSocieteId();
        int currentUserId = GetCurrentUserId();

        IDbContextTransaction? tx = null;
        try
        {
            if (context.Database.IsNpgsql() && context.Database.CurrentTransaction == null)
                tx = await context.Database.BeginTransactionAsync();

            // Verrouillage pessimiste de la ligne Piece sous PostgreSQL pour prévenir les race conditions de stock
            Piece? piece;
            if (context.Database.IsNpgsql())
            {
                piece = await context.Pieces
                    .FromSqlInterpolated($"SELECT * FROM \"Pieces\" WHERE \"Id\" = {mouvement.PieceId} AND \"SocieteId\" = {societeId} FOR UPDATE")
                    .FirstOrDefaultAsync();
            }
            else
            {
                piece = await context.Pieces
                    .FirstOrDefaultAsync(p => p.Id == mouvement.PieceId && p.SocieteId == societeId);
            }

            if (piece == null)
            {
                if (tx != null) await tx.RollbackAsync();
                return NotFound(new { message = "Pièce introuvable dans votre société." });
            }

            if (mouvement.OTId.HasValue && mouvement.OTId.Value > 0)
            {
                var otValid = await context.OrdresTravail.AsNoTracking()
                    .AnyAsync(o => o.Id == mouvement.OTId.Value && o.SocieteId == societeId);
                if (!otValid)
                {
                    if (tx != null) await tx.RollbackAsync();
                    return BadRequest(new { message = "L'ordre de travail référencé n'existe pas dans votre société." });
                }
            }
            else
            {
                mouvement.OTId = null;
            }

            decimal previousStock = piece.StockActuel;
            decimal stockDelta;

            switch (mouvement.Type)
            {
                case TypeMouvementStock.Entree:
                case TypeMouvementStock.Retour:
                    piece.StockActuel += mouvement.Quantite;
                    stockDelta = mouvement.Quantite;
                    break;

                case TypeMouvementStock.Sortie:
                case TypeMouvementStock.Casse:
                    if (piece.StockActuel < mouvement.Quantite)
                    {
                        if (tx != null) await tx.RollbackAsync();
                        return BadRequest(new
                        {
                            message = $"Stock insuffisant pour {piece.Designation} ({piece.StockActuel} disponible(s), {mouvement.Quantite} demandée(s))."
                        });
                    }
                    piece.StockActuel -= mouvement.Quantite;
                    stockDelta = -mouvement.Quantite;
                    break;

                case TypeMouvementStock.Inventaire:
                    // Régularisation suite à comptage physique : Quantite représente le stock physique compté (>= 0)
                    stockDelta = mouvement.Quantite - previousStock;
                    piece.StockActuel = mouvement.Quantite;
                    if (string.IsNullOrWhiteSpace(mouvement.Motif))
                    {
                        var sign = stockDelta >= 0 ? "+" : "";
                        mouvement.Motif = $"Inventaire physique (ancien: {previousStock}, compté: {mouvement.Quantite}, écart: {sign}{stockDelta})";
                    }
                    break;

                default:
                    if (tx != null) await tx.RollbackAsync();
                    return BadRequest(new { message = "Type de mouvement non supporté." });
            }

            if (currentUserId > 0)
                mouvement.UserId = currentUserId;

            if (mouvement.UserId <= 0)
            {
                var fallbackUserId = await context.Users.AsNoTracking()
                    .Where(u => u.SocieteId == societeId && u.IsActive)
                    .OrderBy(u => u.Id)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync();
                if (fallbackUserId > 0)
                    mouvement.UserId = fallbackUserId;
            }

            mouvement.Date = DateTime.UtcNow;
            if (mouvement.PrixUnitaire <= 0)
                mouvement.PrixUnitaire = piece.PrixUnitaire;

            mouvement.PrixTotal = mouvement.Type == TypeMouvementStock.Inventaire
                ? Math.Round(Math.Abs(stockDelta) * mouvement.PrixUnitaire, 2)
                : Math.Round(mouvement.Quantite * mouvement.PrixUnitaire, 2);

            piece.UpdatedAt = DateTime.UtcNow;

            context.MouvementsStock.Add(mouvement);
            await context.SaveChangesAsync();

            if (tx != null) await tx.CommitAsync();

            return CreatedAtAction(nameof(GetAll), new { id = mouvement.Id }, new
            {
                mouvement.Id,
                mouvement.PieceId,
                mouvement.OTId,
                mouvement.UserId,
                mouvement.Type,
                mouvement.Quantite,
                mouvement.PrixUnitaire,
                mouvement.PrixTotal,
                mouvement.Reference,
                mouvement.Motif,
                mouvement.Date,
                stockAvant = previousStock,
                stockApres = piece.StockActuel
            });
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx != null) await tx.DisposeAsync();
        }
    }
}
