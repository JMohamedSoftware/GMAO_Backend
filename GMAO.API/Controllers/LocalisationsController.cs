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
        private readonly GMAO.Infrastructure.Data.GmaoDbContext _context;
        private readonly ITenantContextService _tenantContext;

        public LocalisationsController(
            ILocalisationService localisationService,
            GMAO.Infrastructure.Data.GmaoDbContext context,
            ITenantContextService tenantContext)
        {
            _localisationService = localisationService;
            _context = context;
            _tenantContext = tenantContext;
        }

        private int? GetTargetSocieteId() => _tenantContext.CurrentSocieteId;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Localisation>>> GetAll()
        {
            var targetSid = GetTargetSocieteId();
            var localisations = await _localisationService.GetAllAsync(targetSid);
            return Ok(localisations);
        }

        [HttpGet("tree")]
        public async Task<ActionResult<IEnumerable<Localisation>>> GetTree()
        {
            var targetSid = GetTargetSocieteId();
            var tree = await _localisationService.GetTreeAsync(targetSid);
            return Ok(tree);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Localisation>> GetById(int id)
        {
            var localisation = await _localisationService.GetByIdAsync(id);
            if (localisation == null)
                return NotFound();

            if (!_tenantContext.IsSuperAdmin && localisation.SocieteId != _tenantContext.GetRequiredTenantId())
                return Forbid();
                
            return Ok(localisation);
        }

        [HttpPost]
        public async Task<ActionResult<Localisation>> Create(Localisation localisation)
        {
            localisation.SocieteId = _tenantContext.GetRequiredTenantId();
            var created = await _localisationService.CreateAsync(localisation);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Localisation>> Update(int id, Localisation localisation)
        {
            var existing = await _localisationService.GetByIdAsync(id);
            if (existing == null)
                return NotFound();
            if (!_tenantContext.IsSuperAdmin && existing.SocieteId != _tenantContext.GetRequiredTenantId())
                return Forbid();

            var updated = await _localisationService.UpdateAsync(id, localisation);
            if (updated == null)
                return NotFound();
                
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _localisationService.GetByIdAsync(id);
            if (existing == null)
                return NotFound();
            if (!_tenantContext.IsSuperAdmin && existing.SocieteId != _tenantContext.GetRequiredTenantId())
                return Forbid();

            var success = await _localisationService.DeleteAsync(id);
            if (!success)
                return NotFound();
                
            return NoContent();
        }
    }
}
