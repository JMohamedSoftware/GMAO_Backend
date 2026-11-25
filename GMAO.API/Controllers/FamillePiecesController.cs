using System.Threading.Tasks;
using GMAO.API.Security;
using GMAO.Application.Security;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FamillePiecesController : ControllerBase
{
    private readonly IGenericRepository<FamillePiece> _repository;

    public FamillePiecesController(IGenericRepository<FamillePiece> repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [HasPermission(GmaoPermissions.InventoryView)]
    public async Task<IActionResult> GetAll()
    {
        var familles = await _repository.GetAllAsync();
        return Ok(familles);
    }

    [HttpGet("{id}")]
    [HasPermission(GmaoPermissions.InventoryView)]
    public async Task<IActionResult> GetById(int id)
    {
        var famille = await _repository.GetByIdAsync(id);
        if (famille == null) return NotFound();
        return Ok(famille);
    }

    [HttpPost]
    [HasPermission(GmaoPermissions.InventoryCreate)]
    public async Task<IActionResult> Create([FromBody] FamillePiece famille)
    {
        if (famille == null || string.IsNullOrWhiteSpace(famille.Nom))
            return BadRequest(new { message = "Le nom de la famille de pièces est obligatoire." });

        famille.Nom = famille.Nom.Trim();
        await _repository.AddAsync(famille);
        return CreatedAtAction(nameof(GetById), new { id = famille.Id }, famille);
    }
}
