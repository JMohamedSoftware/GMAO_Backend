using System.Security.Claims;
using GMAO.API.Security;
using GMAO.Application.DTOs.Auth;
using GMAO.Application.Interfaces;
using GMAO.Application.Security;
using GMAO.Application.Services;
using GMAO.Domain.Constants;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly ITenantContextService _tenantContext;
    private readonly GmaoDbContext? _db;

    public UsersController(
        IAuthService authService,
        IUserRepository userRepository,
        ITenantContextService tenantContext,
        GmaoDbContext? db = null)
    {
        _authService = authService;
        _userRepository = userRepository;
        _tenantContext = tenantContext;
        _db = db;
    }

    [HttpGet]
    [HasPermission(GmaoPermissions.UserView)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userRepository.GetAllAsync();

        if (!_tenantContext.IsSuperAdmin)
        {
            var societeId = _tenantContext.GetRequiredTenantId();
            users = users.Where(u => u.Role?.Nom != GmaoRoles.SuperAdmin
                                  && u.RoleId != GmaoRoles.SuperAdminId
                                  && u.SocieteId == societeId);
        }
        else if (_tenantContext.IsImpersonating)
        {
            var targetSocieteId = _tenantContext.GetRequiredTenantId();
            users = users.Where(u => u.Role?.Nom != GmaoRoles.SuperAdmin
                                  && u.RoleId != GmaoRoles.SuperAdminId
                                  && u.SocieteId == targetSocieteId);
        }

        return Ok(users);
    }

    [HttpGet("{id}")]
    [HasPermission(GmaoPermissions.UserView)]
    public async Task<IActionResult> GetUserById(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            return NotFound(new { message = "Utilisateur non trouvé" });

        if (!_tenantContext.IsSuperAdmin)
        {
            var societeId = _tenantContext.GetRequiredTenantId();
            if (user.SocieteId != societeId || user.RoleId == GmaoRoles.SuperAdminId || user.Role?.Nom == GmaoRoles.SuperAdmin)
                return Forbid();
        }
        else if (_tenantContext.IsImpersonating)
        {
            var targetSocieteId = _tenantContext.GetRequiredTenantId();
            if (user.SocieteId != targetSocieteId || user.RoleId == GmaoRoles.SuperAdminId || user.Role?.Nom == GmaoRoles.SuperAdmin)
                return Forbid();
        }

        return Ok(user);
    }

    [HttpPost]
    [HasPermission(GmaoPermissions.UserCreate)]
    public async Task<IActionResult> CreateUser([FromBody] RegisterDto dto)
    {
        if (dto?.User == null)
            return BadRequest(new { message = "Les données utilisateur sont obligatoires." });

        IDbContextTransaction? tx = null;
        try
        {
            if (!_tenantContext.IsSuperAdmin)
            {
                if (dto.User.RoleId == GmaoRoles.SuperAdminId)
                    return Forbid();

                if (!GmaoRoles.IsValidTenantAssignableRoleId(dto.User.RoleId))
                    return BadRequest(new { message = "Le rôle sélectionné n'est pas autorisé pour une société." });

                // Seul un Administrateur ou SuperAdmin peut créer un compte Administrateur
                if (dto.User.RoleId == GmaoRoles.AdministrateurId && !GmaoRoles.IsCompanyAdmin(User))
                    return Forbid();

                dto.User.SocieteId = _tenantContext.GetRequiredTenantId();
            }
            else if (_tenantContext.IsImpersonating)
            {
                dto.User.SocieteId = _tenantContext.GetRequiredTenantId();
                if (dto.User.RoleId == GmaoRoles.SuperAdminId)
                    return BadRequest(new { message = "Impossible d'affecter le rôle SuperAdmin à un compte d'entreprise." });
            }

            if (string.IsNullOrWhiteSpace(dto.User.Nom))
                return BadRequest(new { message = "Le nom est obligatoire." });

            if (_db != null)
            {
                var roleExists = await _db.Roles.AsNoTracking().AnyAsync(r => r.Id == dto.User.RoleId);
                if (!roleExists)
                    return BadRequest(new { message = "Le rôle spécifié est introuvable." });

                if (dto.User.EquipeId.HasValue && dto.User.EquipeId.Value > 0 && dto.User.SocieteId.HasValue)
                {
                    var teamValid = await _db.Equipes.AsNoTracking()
                        .AnyAsync(e => e.Id == dto.User.EquipeId.Value && e.SocieteId == dto.User.SocieteId.Value);
                    if (!teamValid)
                        return BadRequest(new { message = "L'équipe sélectionnée n'appartient pas à cette société." });
                }

                if (dto.User.SocieteId.HasValue && dto.User.SocieteId.Value > 0 && dto.User.IsActive)
                {
                    if (_db.Database.IsNpgsql() && _db.Database.CurrentTransaction == null)
                        tx = await _db.Database.BeginTransactionAsync();

                    var quotaError = await CheckQuotaErrorAsync(dto.User.SocieteId.Value, excludeUserId: null);
                    if (quotaError != null)
                    {
                        if (tx != null) await tx.RollbackAsync();
                        return BadRequest(new { message = quotaError });
                    }
                }
            }

            if (dto.CompetenceIds != null && dto.CompetenceIds.Any())
            {
                foreach (var compId in dto.CompetenceIds.Distinct())
                {
                    dto.User.TechnicienCompetences.Add(new TechnicienCompetence
                    {
                        CompetenceId = compId
                    });
                }
            }

            var user = await _authService.RegisterAsync(dto.User, dto.Password);
            if (tx != null) await tx.CommitAsync();

            return Ok(user);
        }
        catch (System.Exception ex)
        {
            if (tx != null) await tx.RollbackAsync();
            return BadRequest(new { message = ex.Message });
        }
        finally
        {
            if (tx != null) await tx.DisposeAsync();
        }
    }

    [HttpPut("{id}")]
    [HasPermission(GmaoPermissions.UserUpdate)]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound(new { message = "Utilisateur non trouvé" });

        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        bool isSelf = currentUserId == id.ToString();

        if (!_tenantContext.IsSuperAdmin)
        {
            var societeId = _tenantContext.GetRequiredTenantId();
            if (user.SocieteId != societeId || user.RoleId == GmaoRoles.SuperAdminId || user.Role?.Nom == GmaoRoles.SuperAdmin)
                return Forbid();

            if (dto.RoleId == GmaoRoles.SuperAdminId)
                return Forbid();

            // Seul un Administrateur peut modifier un autre Administrateur ou attribuer le rôle Administrateur
            if (!GmaoRoles.IsCompanyAdmin(User) &&
                (user.RoleId == GmaoRoles.AdministrateurId || dto.RoleId == GmaoRoles.AdministrateurId))
            {
                return Forbid();
            }

            if (dto.RoleId.HasValue && !GmaoRoles.IsValidTenantAssignableRoleId(dto.RoleId.Value))
                return BadRequest(new { message = "Le rôle sélectionné n'est pas autorisé pour une société." });
        }
        else if (_tenantContext.IsImpersonating)
        {
            var targetSocieteId = _tenantContext.GetRequiredTenantId();
            if (user.SocieteId != targetSocieteId || user.RoleId == GmaoRoles.SuperAdminId)
                return Forbid();
            if (dto.RoleId == GmaoRoles.SuperAdminId)
                return BadRequest(new { message = "Impossible d'attribuer le rôle SuperAdmin depuis le contexte d'une société." });
        }

        if (isSelf && dto.IsActive == false)
            return BadRequest(new { message = "Vous ne pouvez pas désactiver votre propre compte." });

        if (isSelf && user.RoleId == GmaoRoles.AdministrateurId && dto.RoleId.HasValue && dto.RoleId.Value != GmaoRoles.AdministrateurId)
            return BadRequest(new { message = "Vous ne pouvez pas retirer votre propre rôle d'Administrateur." });

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            if (!normalizedEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existingEmail = await _userRepository.GetByEmailAsync(normalizedEmail);
                if (existingEmail != null && existingEmail.Id != id)
                    return Conflict(new { message = "Cette adresse email est déjà utilisée par un autre compte." });
                user.Email = normalizedEmail;
            }
        }

        IDbContextTransaction? tx = null;
        try
        {
            if (_db != null)
            {
                if (dto.RoleId.HasValue)
                {
                    var roleExists = await _db.Roles.AsNoTracking().AnyAsync(r => r.Id == dto.RoleId.Value);
                    if (!roleExists)
                        return BadRequest(new { message = "Le rôle spécifié est introuvable." });
                }

                if (dto.EquipeId.HasValue && dto.EquipeId.Value > 0 && user.SocieteId.HasValue)
                {
                    var teamValid = await _db.Equipes.AsNoTracking()
                        .AnyAsync(e => e.Id == dto.EquipeId.Value && e.SocieteId == user.SocieteId.Value);
                    if (!teamValid)
                        return BadRequest(new { message = "L'équipe sélectionnée n'appartient pas à cette société." });
                }

                // Vérifier quota lors de la réactivation d'un utilisateur inactif
                if (!user.IsActive && dto.IsActive == true && user.SocieteId.HasValue && user.SocieteId.Value > 0)
                {
                    if (_db.Database.IsNpgsql() && _db.Database.CurrentTransaction == null)
                        tx = await _db.Database.BeginTransactionAsync();

                    var quotaError = await CheckQuotaErrorAsync(user.SocieteId.Value, excludeUserId: user.Id);
                    if (quotaError != null)
                    {
                        if (tx != null) await tx.RollbackAsync();
                        return BadRequest(new { message = quotaError });
                    }
                }
            }

            if (!string.IsNullOrEmpty(dto.Nom)) user.Nom = dto.Nom.Trim();
            if (!string.IsNullOrEmpty(dto.Prenom)) user.Prenom = dto.Prenom.Trim();
            if (dto.Telephone != null) user.Telephone = dto.Telephone;
            if (dto.RoleId.HasValue) user.RoleId = dto.RoleId.Value;
            if (dto.EquipeId.HasValue) user.EquipeId = dto.EquipeId.Value > 0 ? dto.EquipeId.Value : null;
            if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
            if (dto.Avatar != null) user.Avatar = dto.Avatar;
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                if (dto.Password.Length < 6)
                    return BadRequest(new { message = "Le mot de passe doit contenir au moins 6 caractères." });
                user.PasswordHash = PasswordHasher.HashPassword(dto.Password);
            }

            if (_db != null && dto.CompetenceIds != null)
            {
                var existingLinks = await _db.TechnicienCompetences
                    .Where(tc => tc.TechnicienId == user.Id)
                    .ToListAsync();
                _db.TechnicienCompetences.RemoveRange(existingLinks);

                foreach (var compId in dto.CompetenceIds.Distinct())
                {
                    _db.TechnicienCompetences.Add(new TechnicienCompetence
                    {
                        TechnicienId = user.Id,
                        CompetenceId = compId
                    });
                }
            }

            await _userRepository.UpdateAsync(user);
            if (tx != null) await tx.CommitAsync();

            return Ok(user);
        }
        finally
        {
            if (tx != null) await tx.DisposeAsync();
        }
    }

    [HttpDelete("{id}")]
    [HasPermission(GmaoPermissions.UserDelete)]
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
            if (user.SocieteId != societeId || user.RoleId == GmaoRoles.SuperAdminId || user.Role?.Nom == GmaoRoles.SuperAdmin)
                return Forbid();

            if (!GmaoRoles.IsCompanyAdmin(User) && user.RoleId == GmaoRoles.AdministrateurId)
                return Forbid();
        }
        else if (_tenantContext.IsImpersonating)
        {
            var targetSocieteId = _tenantContext.GetRequiredTenantId();
            if (user.SocieteId != targetSocieteId || user.RoleId == GmaoRoles.SuperAdminId)
                return Forbid();
        }

        if (_db != null)
        {
            bool hasReferences =
                await _db.OrdresTravail.IgnoreQueryFilters().AnyAsync(o => o.ResponsableId == id || o.TechnicienId == id) ||
                await _db.DemandesIntervention.IgnoreQueryFilters().AnyAsync(d => d.DemandeurId == id) ||
                await _db.MouvementsStock.IgnoreQueryFilters().AnyAsync(m => m.UserId == id) ||
                await _db.Historiques.AnyAsync(h => h.UserId == id) ||
                await _db.Documents.IgnoreQueryFilters().AnyAsync(d => d.UploadedById == id) ||
                await _db.Equipes.IgnoreQueryFilters().AnyAsync(e => e.ChefEquipeId == id);

            if (hasReferences)
            {
                return Conflict(new
                {
                    message = "Cet utilisateur est référencé par des interventions, demandes, mouvements de stock ou équipes et ne peut pas être supprimé définitivement. Désactivez son compte à la place."
                });
            }
        }

        await _userRepository.DeleteAsync(user);
        return Ok(new { message = "Utilisateur supprimé avec succès." });
    }

    private async Task<string?> CheckQuotaErrorAsync(int societeId, int? excludeUserId)
    {
        if (_db == null) return null;

        Societe? societe;
        if (_db.Database.IsNpgsql())
        {
            societe = await _db.Societes
                .FromSqlInterpolated($"SELECT * FROM \"Societes\" WHERE \"Id\" = {societeId} FOR UPDATE")
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync();
        }
        else
        {
            societe = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == societeId);
        }

        if (societe == null)
            return "La société cible est introuvable.";

        if (!societe.IsActive)
            return "Impossible d'ajouter ou activer un utilisateur dans une société suspendue.";

        if (societe.MaxUsers <= 0)
            return null;

        int activeUsersCount = await _db.Users
            .IgnoreQueryFilters()
            .CountAsync(u => u.SocieteId == societeId
                          && u.IsActive
                          && u.RoleId != GmaoRoles.SuperAdminId
                          && (!excludeUserId.HasValue || u.Id != excludeUserId.Value));

        if (activeUsersCount >= societe.MaxUsers)
            return $"Quota maximum d'utilisateurs atteint ({activeUsersCount}/{societe.MaxUsers}) pour {societe.Nom}.";

        return null;
    }
}
