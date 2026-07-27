using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Application.DTOs.Auth;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;

    public UsersController(IAuthService authService, IUserRepository userRepository)
    {
        _authService = authService;
        _userRepository = userRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userRepository.GetAllAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] RegisterDto dto)
    {
        try
        {
            // Forcer le SocieteId de l'admin connecté
            var societeIdClaim = User.FindFirst("SocieteId")?.Value;
            if (!string.IsNullOrEmpty(societeIdClaim) && int.TryParse(societeIdClaim, out int societeId))
            {
                dto.User.SocieteId = societeId;
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

        if (!string.IsNullOrEmpty(dto.Nom)) user.Nom = dto.Nom;
        if (!string.IsNullOrEmpty(dto.Prenom)) user.Prenom = dto.Prenom;
        if (!string.IsNullOrEmpty(dto.Email)) user.Email = dto.Email;
        if (dto.Telephone != null) user.Telephone = dto.Telephone;
        if (dto.RoleId.HasValue) user.RoleId = dto.RoleId.Value;
        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
        if (dto.Avatar != null) user.Avatar = dto.Avatar;

        await _userRepository.UpdateAsync(user);
        return Ok(user);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (currentUserId == id.ToString())
            return BadRequest(new { message = "Vous ne pouvez pas supprimer votre propre compte." });

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound(new { message = "Utilisateur non trouvé" });

        await _userRepository.DeleteAsync(user);
        return Ok(new { message = "Utilisateur supprimé avec succès." });
    }
}
