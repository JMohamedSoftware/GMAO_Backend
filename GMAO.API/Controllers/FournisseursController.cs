using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FournisseursController : ControllerBase
{
    private readonly IGenericRepository<Fournisseur> _repository;

    public FournisseursController(IGenericRepository<Fournisseur> repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var fournisseurs = await _repository.GetAllAsync();
        return Ok(fournisseurs);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var fournisseur = await _repository.GetByIdAsync(id);
        if (fournisseur == null) return NotFound();
        return Ok(fournisseur);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Fournisseur fournisseur)
    {
        await _repository.AddAsync(fournisseur);
        return CreatedAtAction(nameof(GetById), new { id = fournisseur.Id }, fournisseur);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Fournisseur fournisseur)
    {
        if (id != fournisseur.Id) return BadRequest();
        
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.UpdateAsync(fournisseur);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var fournisseur = await _repository.GetByIdAsync(id);
        if (fournisseur == null) return NotFound();
        
        await _repository.DeleteAsync(fournisseur);
        return NoContent();
    }
}
