using System.Threading.Tasks;
using GMAO.Domain.Interfaces;
using GMAO.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;

    public MaintenanceController(IMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    [HttpPost("declare-breakdown")]
    public async Task<IActionResult> DeclareBreakdown([FromQuery] int equipementId, [FromQuery] int demandeurId, [FromBody] string description)
    {
        var result = await _maintenanceService.DeclareBreakdownAsync(equipementId, demandeurId, description);
        return Ok(result);
    }

    [HttpPost("create-ot")]
    public async Task<IActionResult> CreateOT([FromQuery] int demandId, [FromQuery] int responsableId, [FromQuery] int? technicienId, [FromBody] string description)
    {
        var result = await _maintenanceService.CreateWorkOrderAsync(demandId, responsableId, technicienId, description);
        return Ok(result);
    }

    [HttpPost("complete-ot")]
    public async Task<IActionResult> CompleteOT([FromQuery] int otId, [FromQuery] string diagnostic, [FromQuery] string solution, [FromQuery] decimal tempsPasse)
    {
        var result = await _maintenanceService.CompleteWorkOrderAsync(otId, diagnostic, solution, tempsPasse);
        return Ok(result);
    }
}
