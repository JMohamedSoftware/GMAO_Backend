using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PiecesController : ControllerBase
{
    private readonly IGenericRepository<Piece> _repository;

    public PiecesController(IGenericRepository<Piece> repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var pieces = await _repository.GetAllAsync();
        return Ok(pieces);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var piece = await _repository.GetByIdAsync(id);
        if (piece == null) return NotFound();
        return Ok(piece);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Piece piece)
    {
        await _repository.AddAsync(piece);
        return CreatedAtAction(nameof(GetById), new { id = piece.Id }, piece);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Piece piece)
    {
        if (id != piece.Id) return BadRequest();
        
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(piece);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var piece = await _repository.GetByIdAsync(id);
        if (piece == null) return NotFound();
        
        await _repository.DeleteAsync(piece);
        return NoContent();
    }
}
