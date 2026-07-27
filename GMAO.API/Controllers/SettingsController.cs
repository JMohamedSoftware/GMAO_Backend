using GMAO.Application.DTOs.Settings;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Administrateur")]
public class SettingsController : ControllerBase
{
    private readonly GmaoDbContext _context;

    public SettingsController(GmaoDbContext context)
    {
        _context = context;
    }

    [HttpGet("Roles")]
    public async Task<ActionResult<List<RoleDto>>> GetRoles()
    {
        var roles = await _context.Roles
            .Include(r => r.RolePermissions)
            .ToListAsync();

        var dtos = roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Nom = r.Nom,
            Description = r.Description,
            Permissions = r.RolePermissions.Select(rp => rp.PermissionName).ToList()
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("Roles/{id}/Permissions")]
    public async Task<ActionResult> UpdateRolePermissions(int id, [FromBody] RolePermissionsUpdateDto dto)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null) return NotFound("Role not found");

        // Remove old permissions
        _context.RolePermissions.RemoveRange(role.RolePermissions);

        // Add new permissions
        foreach (var perm in dto.Permissions)
        {
            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionName = perm
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Permissions updated successfully" });
    }

    [AllowAnonymous]
    [HttpPost("SeedMockData")]
    public async Task<ActionResult> SeedMockData()
    {
        // 1. Create Localisations Tree
        var root = new Localisation { Nom = "05 - Usine Tomates POMODORO" };
        _context.Localisations.Add(root);
        await _context.SaveChangesAsync();

        var l1 = new Localisation { Nom = "1 - Réception & Lavage", ParentId = root.Id };
        var l2 = new Localisation { Nom = "2 - Concentration", ParentId = root.Id };
        var l3 = new Localisation { Nom = "3 - Conditionnement & Stérilisation", ParentId = root.Id };
        var l4 = new Localisation { Nom = "4 - Énergie & Utilités", ParentId = root.Id };

        _context.Localisations.AddRange(l1, l2, l3, l4);
        await _context.SaveChangesAsync();

        // 2. Create Equipements
        var e1 = new Equipement
        {
            Id = "EQ-CONV-001", Name = "Convoyeur à bande Réception", Category = "Production", Criticality = "Haute", Status = "En service",
            HealthIndex = 85, LocalisationId = l1.Id, SerialNumber = "SN-001", Brand = "Buhler", CommissionDate = DateTime.UtcNow
        };
        var e2 = new Equipement
        {
            Id = "EQ-EVAP-001", Name = "Évaporateur Concentrateur N°1", Category = "Production", Criticality = "Critique", Status = "En service",
            HealthIndex = 70, LocalisationId = l2.Id, SerialNumber = "SN-002", Brand = "Alfa Laval", CommissionDate = DateTime.UtcNow
        };
        var e3 = new Equipement
        {
            Id = "EQ-PUMP-001", Name = "Pompe Centrifuge LKH-25", Category = "Production", Criticality = "Moyenne", Status = "En panne",
            HealthIndex = 30, LocalisationId = l2.Id, SerialNumber = "SN-003", Brand = "Alfa Laval", CommissionDate = DateTime.UtcNow
        };
        var e4 = new Equipement
        {
            Id = "EQ-AUTO-001", Name = "Autoclave FMC Steril-Host 4", Category = "Production", Criticality = "Critique", Status = "En maintenance",
            HealthIndex = 50, LocalisationId = l3.Id, SerialNumber = "SN-004", Brand = "FMC", CommissionDate = DateTime.UtcNow
        };
        var e5 = new Equipement
        {
            Id = "EQ-BOIL-001", Name = "Chaudière Thermique Babcock VAP 3000", Category = "Utilités", Criticality = "Critique", Status = "En service",
            HealthIndex = 95, LocalisationId = l4.Id, SerialNumber = "SN-005", Brand = "Babcock", CommissionDate = DateTime.UtcNow
        };

        _context.Equipements.AddRange(e1, e2, e3, e4, e5);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Mock data seeded successfully!" });
    }
}
