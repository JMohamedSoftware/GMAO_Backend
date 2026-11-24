namespace GMAO.Domain.Entities;

/// <summary>
/// État de santé, checkpoint LSN et diagnostics réels du Worker CDC PostgreSQL et de son Replication Slot.
/// Reflète honnêtement l'état technique réel (actif, désactivé, ou bloqué par une restriction réseau/plateforme).
/// </summary>
public class CdcWorkerState
{
    public int Id { get; set; }
    public string SlotName { get; set; } = "gmao_cdc_wal_slot";
    public string PublicationName { get; set; } = "gmao_cdc_pub";
    public string Status { get; set; } = "NotConfigured"; // NotConfigured, Disabled, NetworkBlocked, IncompatibleWalLevel, PermissionDenied, ActiveStreaming, Error, Stopped
    public string? StatusReason { get; set; }
    public string? PostgresVersion { get; set; }
    public string? WalLevel { get; set; }
    public int? MaxReplicationSlots { get; set; }
    public int? MaxWalSenders { get; set; }
    public bool? SlotActive { get; set; }
    public string? LastConsumedLsn { get; set; }
    public string? CurrentServerWalLsn { get; set; }
    public long? ReplicationLagBytes { get; set; }
    public long? RetainedWalBytes { get; set; }
    public string? WalStatus { get; set; }
    public long TotalEventsProcessed { get; set; }
    public DateTime? LastEventAtUtc { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
