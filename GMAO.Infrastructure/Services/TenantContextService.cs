using System.Security.Claims;
using System.Security;
using GMAO.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GMAO.Infrastructure.Services;

/// <summary>
/// Implémentation sécurisée du service de contexte multi-tenant.
/// Neutralise formellement l'usurpation de tenant via l'en-tête X-Tenant-Id.
/// </summary>
public class TenantContextService : ITenantContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TenantContextService> _logger;

    public TenantContextService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<TenantContextService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public bool IsSuperAdmin
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return false;
            var role = user.FindFirst(ClaimTypes.Role)?.Value;
            return string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool IsImpersonating
    {
        get
        {
            if (!IsSuperAdmin) return false;
            var impersonatedId = ResolveImpersonatedHeader();
            return impersonatedId.HasValue && impersonatedId.Value > 0;
        }
    }

    public int? CurrentSocieteId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // 1. Cas du SuperAdmin :
            if (IsSuperAdmin)
            {
                var impersonated = ResolveImpersonatedHeader();
                if (impersonated.HasValue && impersonated.Value > 0)
                {
                    return impersonated.Value;
                }
                // SuperAdmin en vue globale transversale (aucun filtre de société spécifique)
                return null;
            }

            // 2. Cas de l'Utilisateur Standard (Client Tenant) :
            // Source d'autorité UNIQUE et INVIOLABLE : le claim JWT SocieteId
            var societeClaim = user.FindFirst("SocieteId")?.Value;
            if (int.TryParse(societeClaim, out int sid) && sid > 0)
            {
                // Contrôle anti-usurpation : Si un utilisateur standard envoie un X-Tenant-Id divergent, on rejette
                var context = _httpContextAccessor.HttpContext;
                if (context != null)
                {
                    var headerVal = context.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim();
                    if (!string.IsNullOrEmpty(headerVal) && headerVal != "null" && headerVal != "undefined")
                    {
                        if (int.TryParse(headerVal, out int headerId) && headerId > 0 && headerId != sid)
                        {
                            var userEmail = user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity.Name;
                            _logger.LogWarning("ALERTE SÉCURITÉ : Tentative d'usurpation de tenant bloquée. Utilisateur {Email} (SocieteId={JwtId}) a envoyé X-Tenant-Id={HeaderId}",
                                userEmail, sid, headerId);
                            throw new SecurityException($"Accès refusé : tentative d'usurpation du tenant {headerId} par un utilisateur non-SuperAdmin.");
                        }
                    }
                }
                return sid;
            }

            return null;
        }
    }

    public int GetRequiredTenantId()
    {
        var tenantId = CurrentSocieteId;
        if (!tenantId.HasValue || tenantId.Value <= 0)
        {
            throw new UnauthorizedAccessException("Contexte locataire (SocieteId) manquant ou non autorisé pour cette opération.");
        }
        return tenantId.Value;
    }

    private int? ResolveImpersonatedHeader()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return null;

        var header = context.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(header) || header == "null" || header == "undefined")
        {
            return null;
        }

        if (int.TryParse(header, out int tenantId) && tenantId > 0)
        {
            return tenantId;
        }

        return null;
    }
}
