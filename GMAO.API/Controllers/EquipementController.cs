using System.Threading.Tasks;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EquipementController : ControllerBase
{
    private readonly IEquipementRepository _equipementRepository;

    public EquipementController(IEquipementRepository equipementRepository)
    {
        _equipementRepository = equipementRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _equipementRepository.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var equipement = await _equipementRepository.GetByIdAsync(id);
        if (equipement == null) return NotFound();
        return Ok(equipement);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Equipement equipement)
    {
        await _equipementRepository.AddAsync(equipement);
        return CreatedAtAction(nameof(GetById), new { id = equipement.Id }, equipement);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Equipement equipement)
    {
        if (id != equipement.Id) return BadRequest();
        await _equipementRepository.UpdateAsync(equipement);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var equipement = await _equipementRepository.GetByIdAsync(id);
        if (equipement == null) return NotFound();
        await _equipementRepository.DeleteAsync(equipement);
        return NoContent();
    }
}
