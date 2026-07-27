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

    [HttpPost("Roles")]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] RoleCreateDto dto)
    {
        var role = new Role
        {
            Nom = dto.Nom,
            Description = dto.Description
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        return Ok(new RoleDto
        {
            Id = role.Id,
            Nom = role.Nom,
            Description = role.Description,
            Permissions = new List<string>()
        });
    }

    [HttpPut("Roles/{id}")]
    public async Task<ActionResult<RoleDto>> UpdateRole(int id, [FromBody] RoleCreateDto dto)
    {
        var role = await _context.Roles.FindAsync(id);
        if (role == null) return NotFound("Role not found");

        if (role.Nom == "SuperAdmin" || role.Nom == "Administrateur")
            return BadRequest("Impossible de modifier les rôles système de base.");

        role.Nom = dto.Nom;
        role.Description = dto.Description;

        await _context.SaveChangesAsync();

        return Ok(new RoleDto
        {
            Id = role.Id,
            Nom = role.Nom,
            Description = role.Description,
            Permissions = await _context.RolePermissions.Where(rp => rp.RoleId == id).Select(rp => rp.PermissionName).ToListAsync()
        });
    }

    [HttpDelete("Roles/{id}")]
    public async Task<ActionResult> DeleteRole(int id)
    {
        var role = await _context.Roles.FindAsync(id);
        if (role == null) return NotFound("Role not found");

        if (role.Nom == "SuperAdmin" || role.Nom == "Administrateur")
            return BadRequest("Impossible de supprimer les rôles système de base.");

        var usersInRole = await _context.Users.AnyAsync(u => u.RoleId == id);
        if (usersInRole)
            return BadRequest("Impossible de supprimer ce rôle car des utilisateurs y sont associés.");

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Role deleted successfully" });
    }
}
