using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Application.Interfaces;
using GMAO.Application.DTOs.Equipes;

namespace GMAO.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EquipesController : ControllerBase
    {
        private readonly IEquipeService _equipeService;
        private readonly ITenantContextService _tenantContext;

        public EquipesController(IEquipeService equipeService, ITenantContextService tenantContext)
        {
            _equipeService = equipeService;
            _tenantContext = tenantContext;
        }

        private int GetCurrentSocieteId() => _tenantContext.GetRequiredTenantId();

        [HttpGet]
        public async Task<IActionResult> GetEquipes()
        {
            var societeId = GetCurrentSocieteId();
            var equipes = await _equipeService.GetEquipesAsync(societeId);
            return Ok(equipes);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetEquipe(int id)
        {
            var societeId = GetCurrentSocieteId();
            var equipe = await _equipeService.GetEquipeByIdAsync(id);
            if (equipe == null || (!_tenantContext.IsSuperAdmin && equipe.SocieteId != societeId))
                return NotFound();
            return Ok(equipe);
        }

        [HttpPost]
        public async Task<IActionResult> CreateEquipe([FromBody] CreateEquipeDto dto)
        {
            var societeId = GetCurrentSocieteId();
            var result = await _equipeService.CreateEquipeAsync(dto, societeId);
            return CreatedAtAction(nameof(GetEquipe), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEquipe(int id, [FromBody] UpdateEquipeDto dto)
        {
            var societeId = GetCurrentSocieteId();
            var existing = await _equipeService.GetEquipeByIdAsync(id);
            if (existing == null || (!_tenantContext.IsSuperAdmin && existing.SocieteId != societeId))
                return NotFound();

            try
            {
                var result = await _equipeService.UpdateEquipeAsync(id, dto);
                return Ok(result);
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEquipe(int id)
        {
            var societeId = GetCurrentSocieteId();
            var existing = await _equipeService.GetEquipeByIdAsync(id);
            if (existing == null || (!_tenantContext.IsSuperAdmin && existing.SocieteId != societeId))
                return NotFound();

            await _equipeService.DeleteEquipeAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/membres/{userId}")]
        public async Task<IActionResult> AddMembre(int id, int userId)
        {
            var societeId = GetCurrentSocieteId();
            var existing = await _equipeService.GetEquipeByIdAsync(id);
            if (existing == null || (!_tenantContext.IsSuperAdmin && existing.SocieteId != societeId))
                return NotFound();

            await _equipeService.AddMembreAsync(id, userId);
            return NoContent();
        }

        [HttpDelete("{id}/membres/{userId}")]
        public async Task<IActionResult> RemoveMembre(int id, int userId)
        {
            var societeId = GetCurrentSocieteId();
            var existing = await _equipeService.GetEquipeByIdAsync(id);
            if (existing == null || (!_tenantContext.IsSuperAdmin && existing.SocieteId != societeId))
                return NotFound();

            await _equipeService.RemoveMembreAsync(id, userId);
            return NoContent();
        }
    }
}
