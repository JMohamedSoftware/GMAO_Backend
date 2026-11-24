using GMAO.Application.DTOs.Settings;
using GMAO.Application.Interfaces;
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
            .ToListAsync();

        var dtos = roles.Select(r =>
        {
            // Vérifier d'abord si l'entreprise a des permissions personnalisées pour ce rôle
            var tenantPerms = r.RolePermissions
                .Where(rp => rp.SocieteId == societeId)
                .Select(rp => rp.PermissionName)
                .ToList();

            // Si aucune personnalisation pour cette entreprise, se baser sur les permissions par défaut système (SocieteId == null)
            var effectivePerms = tenantPerms.Any()
                ? tenantPerms
                : r.RolePermissions
                    .Where(rp => rp.SocieteId == null)
                    .Select(rp => rp.PermissionName)
                    .ToList();

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

        // Supprimer UNIQUEMENT les anciennes permissions propres à cette entreprise
        var existingTenantPerms = await _context.RolePermissions
            .Where(rp => rp.RoleId == id && rp.SocieteId == societeId)
            .ToListAsync();
        _context.RolePermissions.RemoveRange(existingTenantPerms);

        // Ajouter les nouvelles permissions pour cette entreprise (isolation multi-tenant garantie)
        foreach (var perm in dto.Permissions)
        {
            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                SocieteId = societeId,
                PermissionName = perm
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Permissions de l'entreprise enregistrées avec succès" });
    }
}
