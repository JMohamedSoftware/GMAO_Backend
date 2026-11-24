namespace GMAO.Domain.Entities;

/// <summary>
/// Piste d'audit applicative transactionnelle persistée dans PostgreSQL.
/// Capture les opérations métier (utilisateur GMAO, société, entité, valeurs avant/après masquées)
/// avec corrélation transactionnelle PostgreSQL (txid) et corrélation HTTP.
/// </summary>
public class AuditTrailEntry
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserRole { get; set; }
    public int? SocieteId { get; set; }
    public string? SocieteName { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;              // CREATE, UPDATE, DELETE, STATUS_CHANGE, COMPENSATE
    public string BusinessActionLabel { get; set; } = string.Empty; // Libellé métier explicite
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? ChangedProperties { get; set; }
    public string? TransactionId { get; set; }                      // Identifiant transaction PostgreSQL (pg_current_xact_id::xid)
    public string? CorrelationId { get; set; }                      // TraceIdentifier HTTP / corrélation applicative
    public string Source { get; set; } = "API_EF_INTERCEPTOR";
    public string? IpAddress { get; set; }

    // Traçabilité des opérations de compensation métier contrôlées
    public bool IsCompensated { get; set; }
    public long? CompensatedEntryId { get; set; }
    public DateTime? CompensatedAtUtc { get; set; }
    public int? CompensatedByUserId { get; set; }
}
