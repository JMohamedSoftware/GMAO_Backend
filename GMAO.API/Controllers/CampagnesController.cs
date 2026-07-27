using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CampagnesController : ControllerBase
{
    private readonly IGenericRepository<Campagne> _repository;

    public CampagnesController(IGenericRepository<Campagne> repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var campagnes = await _repository.GetAllAsync();
        return Ok(campagnes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var campagne = await _repository.GetByIdAsync(id);
        if (campagne == null) return NotFound();
        return Ok(campagne);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Campagne campagne)
    {
        await _repository.AddAsync(campagne);
        return CreatedAtAction(nameof(GetById), new { id = campagne.Id }, campagne);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Campagne campagne)
    {
        if (id != campagne.Id) return BadRequest();
        
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(campagne);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var campagne = await _repository.GetByIdAsync(id);
        if (campagne == null) return NotFound();
        
        await _repository.DeleteAsync(campagne);
        return NoContent();
    }
}
