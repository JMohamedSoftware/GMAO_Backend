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

        public LocalisationsController(
            ILocalisationService localisationService,
            GMAO.Infrastructure.Data.GmaoDbContext context)
        {
            _localisationService = localisationService;
            _context = context;
        }

        private int? GetTargetSocieteId()
        {
            if (Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader))
            {
                var val = tenantHeader.ToString();
                if (!string.IsNullOrEmpty(val) && val != "null" && val != "undefined")
                {
                    if (int.TryParse(val, out int sid) && sid > 0) return sid;
                    var soc = _context.Societes.FirstOrDefault(s => s.CodeTenant == val);
                    if (soc != null) return soc.Id;
                }
            }

            var societeIdClaim = User.FindFirst("SocieteId")?.Value;
            if (int.TryParse(societeIdClaim, out int claimId) && claimId > 0)
                return claimId;

            if (User.IsInRole("SuperAdmin"))
                return null;

            return 1;
        }

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
                
            return Ok(localisation);
        }

        [HttpPost]
        public async Task<ActionResult<Localisation>> Create(Localisation localisation)
        {
            var targetSid = GetTargetSocieteId();
            localisation.SocieteId = targetSid ?? 1;
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
                return NotFound();
                
            return NoContent();
        }
    }
}
