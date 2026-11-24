using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GMAO.Application.Interfaces;

public class RedoLogQueryParams
{
    public string Source { get; set; } = "ALL"; // ALL, AUDIT_TRAIL, CDC_WAL
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? SocieteId { get; set; }
    public string? TableName { get; set; }
    public string? Operation { get; set; }
    public int? UserId { get; set; }
    public string? UserSearch { get; set; }
    public string? TransactionId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class CdcStatusDto
{
    public string SlotName { get; set; } = string.Empty;
    public string PublicationName { get; set; } = string.Empty;
    public string Status { get; set; } = "NotConfigured";
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
    public long TotalCdcEventsProcessed { get; set; }
    public DateTime? LastEventAtUtc { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public List<string> Alerts { get; set; } = new();
}

public class RedoLogsOverviewDto
{
    public long TotalAuditEvents { get; set; }
    public long TotalCdcEvents { get; set; }
    public long DistinctTransactionsCount { get; set; }
    public long CompensationsCount { get; set; }
    public long EventsLast24h { get; set; }
    public CdcStatusDto CdcWorker { get; set; } = new();
    public List<string> AvailableTables { get; set; } = new();
}

public class RedoLogRecordDto
{
    public string EventKey { get; set; } = string.Empty; // ex: "AUD-123" ou "WAL-456"
    public long NumericId { get; set; }
    public string Source { get; set; } = string.Empty;   // "AUDIT_TRAIL" ou "PostgreSQL_WAL_pgoutput"
    public string Operation { get; set; } = string.Empty; // CREATE / INSERT / UPDATE / DELETE / STATUS_CHANGE / COMPENSATE
    public string TableName { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string BusinessLabel { get; set; } = string.Empty;
    public int? SocieteId { get; set; }
    public string SocieteName { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserRole { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string? TransactionId { get; set; }
    public string? Lsn { get; set; }
    public string? CorrelationId { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? ChangedProperties { get; set; }
    public bool IsCompensated { get; set; }
    public bool CanCompensate { get; set; }
    public string? CompensationNote { get; set; }
    public bool HasWalCorrelation { get; set; }
}

public class PagedRedoLogsResponseDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<RedoLogRecordDto> Items { get; set; } = new();
}

public class CompensateResultDto
{
    public bool Success { get; set; }
    public int HttpStatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public long? OriginalAuditEntryId { get; set; }
    public long? CompensatingAuditEntryId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public interface IRedoLogService
{
    Task<RedoLogsOverviewDto> GetOverviewAsync(CancellationToken ct = default);
    Task<PagedRedoLogsResponseDto> QueryLogsAsync(RedoLogQueryParams query, CancellationToken ct = default);
}

public interface IBusinessCompensationService
{
    bool IsEntryCompensable(string entityName, string action, string? changedProperties, bool isCompensated, out string? reason);
    Task<CompensateResultDto> CompensateAsync(long auditEntryId, int superAdminUserId, string superAdminName, CancellationToken ct = default);
}
