using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

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

    [HttpGet]
    public async Task<IActionResult> GetAll([FromServices] GMAO.Infrastructure.Data.GmaoDbContext context)
    {
        var mouvements = await context.MouvementsStock
            .Include(m => m.Piece)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
        return Ok(mouvements);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MouvementStock mouvement)
    {
        mouvement.Date = System.DateTime.UtcNow;
        await _repository.AddAsync(mouvement);
        return CreatedAtAction(nameof(GetAll), new { id = mouvement.Id }, mouvement);
    }
}
