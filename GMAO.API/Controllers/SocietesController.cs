using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class SocietesController : ControllerBase
{
    private readonly IGenericRepository<Societe> _repository;

    public SocietesController(IGenericRepository<Societe> repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var societes = await _repository.GetAllAsync();
        return Ok(societes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var societe = await _repository.GetByIdAsync(id);
        if (societe == null) return NotFound();
        return Ok(societe);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Societe societe)
    {
        if (string.IsNullOrWhiteSpace(societe.CodeTenant))
        {
            return BadRequest("Le code tenant est obligatoire.");
        }
        if (string.IsNullOrWhiteSpace(societe.Nom))
        {
            return BadRequest("Le nom de la société est obligatoire.");
        }

        societe.CreatedAt = DateTime.UtcNow;
        await _repository.AddAsync(societe);
        return CreatedAtAction(nameof(GetById), new { id = societe.Id }, societe);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Societe societe)
    {
        if (id != societe.Id) return BadRequest("L'id dans l'URL ne correspond pas au corps.");

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.Nom = societe.Nom;
        existing.CodeTenant = societe.CodeTenant;
        existing.Adresse = societe.Adresse;
        existing.EmailContact = societe.EmailContact;
        existing.SubscriptionPlan = societe.SubscriptionPlan;
        existing.CapacityTonsPerDay = societe.CapacityTonsPerDay;
        existing.IsActive = societe.IsActive;

        await _repository.UpdateAsync(existing);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _repository.DeleteAsync(existing);
        return NoContent();
    }
}
