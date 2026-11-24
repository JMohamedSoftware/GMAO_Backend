namespace GMAO.Domain.Entities;

/// <summary>
/// Événement réel capturé exclusivement depuis le Write-Ahead Log (WAL) PostgreSQL
/// via le décodeur logique pgoutput (Logical Replication Slot).
/// Aucune donnée simulée ou fabriquée ne doit jamais être persistée dans cette table.
/// </summary>
public class CdcEvent
{
    public long Id { get; set; }
    public string Operation { get; set; } = string.Empty;           // INSERT, UPDATE, DELETE
    public string SchemaName { get; set; } = "public";
    public string TableName { get; set; } = string.Empty;
    public string? RecordId { get; set; }
    public int? SocieteId { get; set; }
    public string TransactionId { get; set; } = string.Empty;       // xid PostgreSQL issu de BeginMessage
    public string Lsn { get; set; } = string.Empty;                 // Log Sequence Number réel issu de PostgreSQL
    public DateTime CommitTimestampUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public string? OldValuesJson { get; set; }                      // Colonnes masquées si sensibles
    public string? NewValuesJson { get; set; }                      // Colonnes masquées si sensibles
    public string SlotName { get; set; } = string.Empty;
    public string PublicationName { get; set; } = string.Empty;
    public string Source { get; set; } = "PostgreSQL_WAL_pgoutput";
}
