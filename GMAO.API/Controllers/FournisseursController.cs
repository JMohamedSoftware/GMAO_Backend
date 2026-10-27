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

    private int GetSocieteId()
    {
        var societeIdClaim = User.FindFirst("SocieteId")?.Value;
        if (int.TryParse(societeIdClaim, out int societeId) && societeId > 0)
            return societeId;
        return 1;
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
        if (string.IsNullOrWhiteSpace(fournisseur.Nom))
            return BadRequest(new { message = "Le nom du fournisseur est obligatoire." });

        fournisseur.SocieteId = GetSocieteId();
        if (string.IsNullOrWhiteSpace(fournisseur.Code))
        {
            fournisseur.Code = $"FRN-{DateTime.UtcNow.Ticks % 10000:D4}";
        }

        await _repository.AddAsync(fournisseur);
        return CreatedAtAction(nameof(GetById), new { id = fournisseur.Id }, fournisseur);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Fournisseur fournisseur)
    {
        if (id != fournisseur.Id) return BadRequest();
        
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.Nom = fournisseur.Nom;
        existing.Contact = fournisseur.Contact;
        existing.Telephone = fournisseur.Telephone;
        existing.Email = fournisseur.Email;
        existing.Adresse = fournisseur.Adresse;
        existing.IsActive = fournisseur.IsActive;

        await _repository.UpdateAsync(existing);
        return Ok(existing);
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
