using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Application.DTOs.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System;

namespace GMAO.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IGenericRepository<RefreshToken> _refreshTokenRepository;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        IGenericRepository<RefreshToken> refreshTokenRepository,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto> LoginAsync(string email, string password)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null || !user.IsActive)
            throw new Exception("Identifiants incorrects ou compte inactif.");

        if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            throw new Exception("Identifiants incorrects.");

        var accessToken = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        var rt = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7"))
        };

        await _refreshTokenRepository.AddAsync(rt);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = MapToDto(user)
        };
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
    {
        var tokens = await _refreshTokenRepository.GetAllAsync();
        var rt = tokens.FirstOrDefault(x => x.Token == refreshToken);

        if (rt == null || rt.IsRevoked || rt.ExpiresAt < DateTime.UtcNow)
            throw new Exception("Session expirée ou invalide. Veuillez vous reconnecter.");

        var user = await _userRepository.GetByIdAsync(rt.UserId);
        if (user == null || !user.IsActive)
            throw new Exception("Utilisateur introuvable ou inactif.");

        // Invalidate old token
        rt.IsRevoked = true;
        await _refreshTokenRepository.UpdateAsync(rt);

        // Generate new tokens
        var newAccessToken = GenerateJwtToken(user);
        var newRefreshToken = GenerateRefreshToken();

        var newRt = new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7"))
        };

        await _refreshTokenRepository.AddAsync(newRt);

        // Map user with roles via GetByEmail since GetById might not include Roles natively if not configured
        var fullUser = await _userRepository.GetByEmailAsync(user.Email);

        return new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            User = MapToDto(fullUser ?? user)
        };
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var tokens = await _refreshTokenRepository.GetAllAsync();
        var rt = tokens.FirstOrDefault(x => x.Token == refreshToken);
        if (rt != null)
        {
            rt.IsRevoked = true;
            await _refreshTokenRepository.UpdateAsync(rt);
        }
    }

    public async Task<UserProfileDto> GetMeAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new Exception("Utilisateur introuvable.");
        
        var fullUser = await _userRepository.GetByEmailAsync(user.Email);
        return MapToDto(fullUser ?? user);
    }

    public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new Exception("Utilisateur introuvable.");

        if (!PasswordHasher.VerifyPassword(oldPassword, user.PasswordHash))
            throw new Exception("L'ancien mot de passe est incorrect.");

        user.PasswordHash = PasswordHasher.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task<User> RegisterAsync(User user, string password)
    {
        user.PasswordHash = PasswordHasher.HashPassword(password);
        await _userRepository.AddAsync(user);
        return user;
    }

    // --- Helpers ---
    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role?.Nom ?? "Utilisateur"),
            new Claim("name", $"{user.Prenom} {user.Nom}")
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(jwtSettings["AccessTokenExpirationMinutes"] ?? "60")),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private UserProfileDto MapToDto(User user)
    {
        if (user.Societe == null)
            throw new Exception("Cet utilisateur n'est rattaché à aucune société.");

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            Name = $"{user.Prenom} {user.Nom}",
            Role = user.Role?.Nom ?? "Utilisateur",
            TenantId = user.Societe.CodeTenant,
            TenantName = user.Societe.Nom,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt.ToString("o")
        };
    }
}
