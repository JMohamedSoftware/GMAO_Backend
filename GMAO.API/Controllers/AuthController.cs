using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Application.DTOs.Auth;
using GMAO.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        // Squelette à connecter plus tard
        return Ok(await _authService.LoginAsync(dto.Email, dto.Password));
    }


    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromQuery] int userId, [FromBody] ChangePasswordDto dto)
    {
        // Squelette à connecter plus tard
        return Ok(await _authService.ChangePasswordAsync(userId, dto.OldPassword, dto.NewPassword));
    }
}
