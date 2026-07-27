using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdresTravailController : ControllerBase
{
    private readonly IGenericRepository<OrdresTravail> _repository;

    public OrdresTravailController(IGenericRepository<OrdresTravail> repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var ordres = await _repository.GetAllAsync();
        return Ok(ordres);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var ordre = await _repository.GetByIdAsync(id);
        if (ordre == null) return NotFound();
        return Ok(ordre);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrdresTravail ordre)
    {
        await _repository.AddAsync(ordre);
        return CreatedAtAction(nameof(GetById), new { id = ordre.Id }, ordre);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] OrdresTravail ordre)
    {
        if (id != ordre.Id) return BadRequest();
        
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(ordre);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ordre = await _repository.GetByIdAsync(id);
        if (ordre == null) return NotFound();
        
        await _repository.DeleteAsync(ordre);
        return NoContent();
    }
}
