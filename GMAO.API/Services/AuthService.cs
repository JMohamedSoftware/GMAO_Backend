using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GMAO.Application.DTOs.Auth;
using GMAO.Application.Interfaces;
using GMAO.Application.Security;
using GMAO.Domain.Constants;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GMAO.Application.Services;

public class AuthService : IAuthService
{
    private const string ExplicitEmptyPermissionMarker = "__NONE__";

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
        var normalizedEmail = (email ?? "").Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (user == null || !user.IsActive)
            throw new Exception("Identifiants incorrects ou compte inactif.");

        if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            throw new Exception("Identifiants incorrects.");

        // Migration transparente des anciens hash SHA-256 vérifiés vers BCrypt
        if (PasswordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = PasswordHasher.HashPassword(password);
            await _userRepository.UpdateAsync(user);
        }

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

        rt.IsRevoked = true;
        await _refreshTokenRepository.UpdateAsync(rt);

        var fullUser = await _userRepository.GetByEmailAsync(user.Email) ?? user;

        var newAccessToken = GenerateJwtToken(fullUser);
        var newRefreshToken = GenerateRefreshToken();

        var newRt = new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7"))
        };

        await _refreshTokenRepository.AddAsync(newRt);

        return new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            User = MapToDto(fullUser)
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
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw new Exception("Le nouveau mot de passe doit contenir au moins 6 caractères.");

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
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new Exception("Le mot de passe doit contenir au moins 6 caractères.");

        user.Email = (user.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(user.Email))
            throw new Exception("L'adresse email est obligatoire.");

        var existingUser = await _userRepository.GetByEmailAsync(user.Email);
        if (existingUser != null)
            throw new Exception("L'email est déjà utilisé.");

        user.PasswordHash = PasswordHasher.HashPassword(password);
        await _userRepository.AddAsync(user);
        return user;
    }

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
            new Claim("name", $"{user.Prenom} {user.Nom}".Trim()),
            new Claim("SocieteId", user.SocieteId?.ToString() ?? ""),
            new Claim("EquipeId", user.EquipeId?.ToString() ?? ""),
            new Claim(GmaoPermissions.PermissionsLoadedClaim, "true")
        };

        var effectivePermissions = GetEffectivePermissions(user);
        foreach (var perm in effectivePermissions)
        {
            claims.Add(new Claim("Permission", perm));
        }

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(jwtSettings["AccessTokenExpirationMinutes"] ?? "60")),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static List<string> GetEffectivePermissions(User user)
    {
        var rolePermissions = user.Role?.RolePermissions;

        if (rolePermissions != null && user.SocieteId.HasValue && user.SocieteId.Value > 0)
        {
            var tenantPerms = rolePermissions
                .Where(rp => rp.SocieteId == user.SocieteId.Value)
                .Select(rp => rp.PermissionName)
                .ToList();

            if (tenantPerms.Any())
                return tenantPerms.Where(p => p != ExplicitEmptyPermissionMarker).ToList();
        }

        if (rolePermissions != null)
        {
            var globalPerms = rolePermissions
                .Where(rp => rp.SocieteId == null)
                .Select(rp => rp.PermissionName)
                .ToList();

            if (globalPerms.Any())
                return globalPerms.Where(p => p != ExplicitEmptyPermissionMarker).ToList();
        }

        return GmaoPermissions.GetDefaultPermissionsForRole(user.Role?.Nom).ToList();
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private static UserProfileDto MapToDto(User user)
    {
        var roleName = user.Role?.Nom ?? "Utilisateur";

        if (user.Societe == null && roleName != GmaoRoles.SuperAdmin)
            throw new Exception("Cet utilisateur n'est rattaché à aucune société.");

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            Name = $"{user.Prenom} {user.Nom}".Trim(),
            Role = roleName,
            TenantId = user.Societe?.CodeTenant ?? "platform",
            TenantName = user.Societe?.Nom ?? "Plateforme GMAO",
            EquipeId = user.EquipeId,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt.ToString("o"),
            Permissions = GetEffectivePermissions(user)
        };
    }
}
