using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;

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
    public async Task<IActionResult> GetAll()
    {
        var familles = await _repository.GetAllAsync();
        return Ok(familles);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var famille = await _repository.GetByIdAsync(id);
        if (famille == null) return NotFound();
        return Ok(famille);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FamillePiece famille)
    {
        await _repository.AddAsync(famille);
        return CreatedAtAction(nameof(GetById), new { id = famille.Id }, famille);
    }
}
