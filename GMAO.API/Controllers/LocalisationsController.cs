using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;

namespace GMAO.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LocalisationsController : ControllerBase
    {
        private readonly ILocalisationService _localisationService;

        public LocalisationsController(ILocalisationService localisationService)
        {
            _localisationService = localisationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Localisation>>> GetAll()
        {
            var localisations = await _localisationService.GetAllAsync();
            return Ok(localisations);
        }

        [HttpGet("tree")]
        public async Task<ActionResult<IEnumerable<Localisation>>> GetTree()
        {
            var tree = await _localisationService.GetTreeAsync();
            return Ok(tree);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Localisation>> GetById(int id)
        {
            var localisation = await _localisationService.GetByIdAsync(id);
            if (localisation == null)
                return NotFound();
                
            return Ok(localisation);
        }

        [HttpPost]
        public async Task<ActionResult<Localisation>> Create(Localisation localisation)
        {
            var created = await _localisationService.CreateAsync(localisation);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Localisation>> Update(int id, Localisation localisation)
        {
            var updated = await _localisationService.UpdateAsync(id, localisation);
            if (updated == null)
                return NotFound();
                
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _localisationService.DeleteAsync(id);
            if (!success)
                return BadRequest("Impossible de supprimer cette localisation (elle contient peut-être des équipements ou des sous-localisations).");
                
            return NoContent();
        }
    }
}
