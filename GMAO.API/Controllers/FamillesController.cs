using System.Threading.Tasks;
using System.Linq;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FamillesController : ControllerBase
{
    private readonly GmaoDbContext _context;

    public FamillesController(GmaoDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var familles = await _context.Familles.OrderBy(f => f.Id).ToListAsync();
        return Ok(familles);
    }
}
