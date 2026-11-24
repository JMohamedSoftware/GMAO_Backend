namespace GMAO.Application.Interfaces;

/// <summary>
/// Service centralisé de résolution et de gestion du contexte multi-tenant (Société).
/// Garantit qu'un utilisateur standard ne peut jamais accéder aux données d'un autre tenant.
/// </summary>
public interface ITenantContextService
{
    /// <summary>
    /// Identifiant de l'entreprise active pour la requête courante.
    /// Pour un utilisateur standard : extrait strictement du claim JWT "SocieteId".
    /// Pour un SuperAdmin : null en mode transversal global, ou l'ID de l'entreprise impersonnée.
    /// Retourne null si aucun contexte n'est résolu (ex: non authentifié).
    /// </summary>
    int? CurrentSocieteId { get; }

    /// <summary>
    /// Indique si l'utilisateur connecté possède le rôle SuperAdmin.
    /// </summary>
    bool IsSuperAdmin { get; }

    /// <summary>
    /// Indique si un SuperAdmin est actuellement en train d'impersoner une société via X-Tenant-Id.
    /// Toujours false pour un utilisateur standard.
    /// </summary>
    bool IsImpersonating { get; }

    /// <summary>
    /// Retourne l'identifiant du tenant requis pour les opérations de modification/création.
    /// Lève une exception (401/403) si le contexte tenant est absent ou invalide.
    /// Ne retourne JAMAIS de fallback arbitraire (ex: 1).
    /// </summary>
    int GetRequiredTenantId();
}
