using GMAO.Application.DTOs.Settings;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
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
}
