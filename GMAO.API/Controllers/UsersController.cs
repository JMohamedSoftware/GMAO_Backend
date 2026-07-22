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
        
        return Ok(await _authService.RegisterAsync(dto.User, dto.Password));
    }
}
