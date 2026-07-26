using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GMAO.Application.Interfaces;
using GMAO.Application.DTOs.Equipes;
using System.Security.Claims;
using System.Linq;

namespace GMAO.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Should require Admin or specific permissions later
    public class EquipesController : ControllerBase
    {
        private readonly IEquipeService _equipeService;

        public EquipesController(IEquipeService equipeService)
        {
            _equipeService = equipeService;
        }

        private int GetCurrentSocieteId()
        {
            // For now, assuming default societe 1 or parsing from token
            var societeClaim = User.Claims.FirstOrDefault(c => c.Type == "SocieteId")?.Value;
            return int.TryParse(societeClaim, out var sId) ? sId : 1;
        }

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
            var equipe = await _equipeService.GetEquipeByIdAsync(id);
            if (equipe == null || equipe.SocieteId != GetCurrentSocieteId()) return NotFound();
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
            await _equipeService.DeleteEquipeAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/membres/{userId}")]
        public async Task<IActionResult> AddMembre(int id, int userId)
        {
            await _equipeService.AddMembreAsync(id, userId);
            return NoContent();
        }

        [HttpDelete("{id}/membres/{userId}")]
        public async Task<IActionResult> RemoveMembre(int id, int userId)
        {
            await _equipeService.RemoveMembreAsync(id, userId);
            return NoContent();
        }
    }
}
