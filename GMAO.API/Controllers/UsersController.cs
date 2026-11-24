using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.Application.DTOs.Auth;
using GMAO.Application.Interfaces;
using GMAO.Application.Services;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly ITenantContextService _tenantContext;

    public UsersController(
        IAuthService authService,
        IUserRepository userRepository,
        ITenantContextService tenantContext)
    {
        _authService = authService;
        _userRepository = userRepository;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userRepository.GetAllAsync();

        if (!_tenantContext.IsSuperAdmin)
        {
            var societeId = _tenantContext.GetRequiredTenantId();
            users = users.Where(u => u.Role?.Nom != "SuperAdmin"
                                  && u.RoleId != 7
                                  && u.SocieteId == societeId);
        }
        else if (_tenantContext.IsImpersonating)
        {
            var targetSocieteId = _tenantContext.GetRequiredTenantId();
            users = users.Where(u => u.Role?.Nom != "SuperAdmin"
                                  && u.RoleId != 7
                                  && u.SocieteId == targetSocieteId);
        }

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] RegisterDto dto)
    {
        try
        {
            if (!_tenantContext.IsSuperAdmin)
            {
                if (dto.User.RoleId == 7)
                    return Forbid();

                dto.User.SocieteId = _tenantContext.GetRequiredTenantId();
            }
            else if (_tenantContext.IsImpersonating)
            {
                dto.User.SocieteId = _tenantContext.GetRequiredTenantId();
            }

            if (dto.CompetenceIds != null && dto.CompetenceIds.Any())
            {
                foreach (var compId in dto.CompetenceIds)
                {
                    dto.User.TechnicienCompetences.Add(new TechnicienCompetence
                    {
                        CompetenceId = compId
                    });
                }
            }

            var user = await _authService.RegisterAsync(dto.User, dto.Password);
            return Ok(user);
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound(new { message = "Utilisateur non trouvé" });

        if (!_tenantContext.IsSuperAdmin)
        {
            var societeId = _tenantContext.GetRequiredTenantId();
            if (user.SocieteId != societeId || user.RoleId == 7 || user.Role?.Nom == "SuperAdmin")
                return Forbid();

            if (dto.RoleId == 7)
                return Forbid();
        }

        if (!string.IsNullOrEmpty(dto.Nom)) user.Nom = dto.Nom;
        if (!string.IsNullOrEmpty(dto.Prenom)) user.Prenom = dto.Prenom;
        if (!string.IsNullOrEmpty(dto.Email)) user.Email = dto.Email;
        if (dto.Telephone != null) user.Telephone = dto.Telephone;
        if (dto.RoleId.HasValue) user.RoleId = dto.RoleId.Value;
        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
        if (dto.Avatar != null) user.Avatar = dto.Avatar;
        if (!string.IsNullOrWhiteSpace(dto.Password)) user.PasswordHash = PasswordHasher.HashPassword(dto.Password);

        await _userRepository.UpdateAsync(user);
        return Ok(user);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (currentUserId == id.ToString())
            return BadRequest(new { message = "Vous ne pouvez pas supprimer votre propre compte." });

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound(new { message = "Utilisateur non trouvé" });

        if (!_tenantContext.IsSuperAdmin)
        {
            var societeId = _tenantContext.GetRequiredTenantId();
            if (user.SocieteId != societeId || user.RoleId == 7 || user.Role?.Nom == "SuperAdmin")
                return Forbid();
        }

        await _userRepository.DeleteAsync(user);
        return Ok(new { message = "Utilisateur supprimé avec succès." });
    }
}
