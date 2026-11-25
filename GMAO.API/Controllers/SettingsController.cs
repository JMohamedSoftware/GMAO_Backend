using GMAO.API.Security;
using GMAO.Application.DTOs.Settings;
using GMAO.Application.Interfaces;
using GMAO.Application.Security;
using GMAO.Domain.Constants;
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
    private const string ExplicitEmptyPermissionMarker = "__NONE__";

    private readonly GmaoDbContext _context;
    private readonly ITenantContextService _tenantContext;

    public SettingsController(GmaoDbContext context, ITenantContextService tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    private int GetSocieteId() => _tenantContext.GetRequiredTenantId();

    [HttpGet("Roles")]
    public async Task<ActionResult<List<RoleDto>>> GetRoles()
    {
        var societeId = GetSocieteId();
        var roles = await _context.Roles
            .Include(r => r.RolePermissions)
            .OrderBy(r => r.Id)
            .ToListAsync();

        var dtos = roles
            .Where(r => r.Nom != GmaoRoles.SuperAdmin && r.Id != GmaoRoles.SuperAdminId)
            .Select(r =>
            {
                var tenantRows = r.RolePermissions
                    .Where(rp => rp.SocieteId == societeId)
                    .Select(rp => rp.PermissionName)
                    .ToList();

                List<string> effectivePerms;
                if (tenantRows.Any())
                {
                    effectivePerms = tenantRows.Where(p => p != ExplicitEmptyPermissionMarker).ToList();
                }
                else
                {
                    var globalRows = r.RolePermissions
                        .Where(rp => rp.SocieteId == null)
                        .Select(rp => rp.PermissionName)
                        .ToList();

                    effectivePerms = globalRows.Any()
                        ? globalRows.Where(p => p != ExplicitEmptyPermissionMarker).ToList()
                        : GmaoPermissions.GetDefaultPermissionsForRole(r.Nom).ToList();
                }

                return new RoleDto
                {
                    Id = r.Id,
                    Nom = r.Nom,
                    Description = r.Description,
                    Permissions = effectivePerms
                };
            }).ToList();

        return Ok(dtos);
    }

    [HttpPost("Roles/{id}/Permissions")]
    public async Task<ActionResult> UpdateRolePermissions(int id, [FromBody] RolePermissionsUpdateDto dto)
    {
        var societeId = GetSocieteId();
        var role = await _context.Roles.FindAsync(id);

        if (role == null) return NotFound("Role not found");
        if (role.Id == GmaoRoles.SuperAdminId || role.Nom == GmaoRoles.SuperAdmin ||
            role.Id == GmaoRoles.AdministrateurId || role.Nom == GmaoRoles.Administrateur)
        {
            return Forbid();
        }

        var existingTenantPerms = await _context.RolePermissions
            .Where(rp => rp.RoleId == id && rp.SocieteId == societeId)
            .ToListAsync();
        _context.RolePermissions.RemoveRange(existingTenantPerms);

        var cleanedPerms = (dto.Permissions ?? new List<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != ExplicitEmptyPermissionMarker)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleanedPerms.Count == 0)
        {
            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                SocieteId = societeId,
                PermissionName = ExplicitEmptyPermissionMarker
            });
        }
        else
        {
            foreach (var perm in cleanedPerms)
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    SocieteId = societeId,
                    PermissionName = perm
                });
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Permissions de l'entreprise enregistrées avec succès" });
    }
}
