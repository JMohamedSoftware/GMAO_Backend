using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GMAO.Infrastructure.Audit;

/// <summary>
/// Service de consultation paginée et de supervision de l'Audit Trail applicatif
/// et du flux PostgreSQL WAL / CDC réel (CdcEvents + CdcWorkerStates).
/// Aucune donnée simulée ou inventée n'est retournée.
/// </summary>
public class RedoLogService : IRedoLogService
{
    private readonly GmaoDbContext _db;
    private readonly IBusinessCompensationService _compensationService;

    public RedoLogService(GmaoDbContext db, IBusinessCompensationService compensationService)
    {
        _db = db;
        _compensationService = compensationService;
    }

    public async Task<RedoLogsOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var last24h = now.AddHours(-24);

        var totalAudit = await _db.AuditTrailEntries.AsNoTracking().LongCountAsync(ct);
        var totalCdc = await _db.CdcEvents.AsNoTracking().LongCountAsync(ct);
        var compCount = await _db.AuditTrailEntries.AsNoTracking().LongCountAsync(a => a.Action == "COMPENSATE", ct);
        var audit24h = await _db.AuditTrailEntries.AsNoTracking().LongCountAsync(a => a.TimestampUtc >= last24h, ct);
        var cdc24h = await _db.CdcEvents.AsNoTracking().LongCountAsync(c => c.CommitTimestampUtc >= last24h, ct);

        var distinctAuditTx = await _db.AuditTrailEntries.AsNoTracking()
            .Where(a => a.TransactionId != null)
            .Select(a => a.TransactionId!)
            .Distinct()
            .LongCountAsync(ct);

        var distinctCdcTx = await _db.CdcEvents.AsNoTracking()
            .Select(c => c.TransactionId)
            .Distinct()
            .LongCountAsync(ct);

        var workerState = await _db.CdcWorkerStates.AsNoTracking()
            .OrderByDescending(w => w.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var alerts = new List<string>();
        var cdcDto = new CdcStatusDto();

        if (workerState == null)
        {
            cdcDto.Status = "NotInitialized";
            cdcDto.StatusReason = "Le Worker CDC PostgreSQL n'a pas encore exécuté sa sonde de démarrage.";
            alerts.Add("Flux WAL / CDC non initialisé — Seul l'Audit Trail applicatif est actif.");
        }
        else
        {
            cdcDto.SlotName = workerState.SlotName;
            cdcDto.PublicationName = workerState.PublicationName;
            cdcDto.Status = workerState.Status;
            cdcDto.StatusReason = workerState.StatusReason;
            cdcDto.PostgresVersion = workerState.PostgresVersion;
            cdcDto.WalLevel = workerState.WalLevel;
            cdcDto.MaxReplicationSlots = workerState.MaxReplicationSlots;
            cdcDto.MaxWalSenders = workerState.MaxWalSenders;
            cdcDto.SlotActive = workerState.SlotActive;
            cdcDto.LastConsumedLsn = workerState.LastConsumedLsn;
            cdcDto.CurrentServerWalLsn = workerState.CurrentServerWalLsn;
            cdcDto.ReplicationLagBytes = workerState.ReplicationLagBytes;
            cdcDto.RetainedWalBytes = workerState.RetainedWalBytes;
            cdcDto.WalStatus = workerState.WalStatus;
            cdcDto.TotalCdcEventsProcessed = workerState.TotalEventsProcessed;
            cdcDto.LastEventAtUtc = workerState.LastEventAtUtc;
            cdcDto.LastHeartbeatUtc = workerState.LastHeartbeatUtc;

            if (workerState.Status != "ActiveStreaming")
            {
                alerts.Add($"Composant CDC WAL non actif ({workerState.Status}) : {workerState.StatusReason}");
            }
            if (workerState.ReplicationLagBytes.HasValue && workerState.ReplicationLagBytes.Value > 50 * 1024 * 1024)
            {
                alerts.Add($"Retard de consommation WAL élevé : {workerState.ReplicationLagBytes.Value / (1024 * 1024):0.##} Mo");
            }
            if (workerState.RetainedWalBytes.HasValue && workerState.RetainedWalBytes.Value > 200 * 1024 * 1024)
            {
                alerts.Add($"Rétention WAL critique sur disque : {workerState.RetainedWalBytes.Value / (1024 * 1024):0.##} Mo");
            }
            if (string.Equals(workerState.WalStatus, "lost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(workerState.WalStatus, "unreserved", StringComparison.OrdinalIgnoreCase))
            {
                alerts.Add($"Statut critique du Replication Slot : {workerState.WalStatus}");
            }
        }
        cdcDto.Alerts = alerts;

        var auditTables = await _db.AuditTrailEntries.AsNoTracking()
            .Select(a => a.EntityName)
            .Distinct()
            .ToListAsync(ct);
        var cdcTables = await _db.CdcEvents.AsNoTracking()
            .Select(c => c.TableName)
            .Distinct()
            .ToListAsync(ct);

        return new RedoLogsOverviewDto
        {
            TotalAuditEvents = totalAudit,
            TotalCdcEvents = totalCdc,
            DistinctTransactionsCount = Math.Max(distinctAuditTx, distinctCdcTx),
            CompensationsCount = compCount,
            EventsLast24h = audit24h + cdc24h,
            CdcWorker = cdcDto,
            AvailableTables = auditTables.Union(cdcTables).OrderBy(t => t).ToList()
        };
    }

    public async Task<PagedRedoLogsResponseDto> QueryLogsAsync(RedoLogQueryParams query, CancellationToken ct = default)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);
        int skip = (page - 1) * pageSize;

        var societeNames = await _db.Societes.IgnoreQueryFilters()
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.Nom, ct);

        bool includeAudit = query.Source is "ALL" or "AUDIT_TRAIL";
        bool includeCdc = query.Source is "ALL" or "CDC_WAL";

        // 1. Audit Trail (source primaire applicative ou corrélée)
        if (query.Source == "CDC_WAL")
        {
            var cdcQ = BuildCdcQuery(query);
            long total = await cdcQ.LongCountAsync(ct);
            var rows = await cdcQ
                .OrderByDescending(c => c.CommitTimestampUtc)
                .ThenByDescending(c => c.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            var txIds = rows.Select(r => r.TransactionId).Distinct().ToList();
            var matchingAudits = await _db.AuditTrailEntries.AsNoTracking()
                .Where(a => a.TransactionId != null && txIds.Contains(a.TransactionId))
                .ToListAsync(ct);

            var items = rows.Select(c =>
            {
                var linkedAudit = matchingAudits.FirstOrDefault(a =>
                    a.TransactionId == c.TransactionId &&
                    string.Equals(a.EntityName, c.TableName, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(c.RecordId) || a.EntityId == c.RecordId));

                return MapCdcToDto(c, linkedAudit, societeNames);
            }).ToList();

            return new PagedRedoLogsResponseDto
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Items = items
            };
        }

        if (query.Source == "AUDIT_TRAIL")
        {
            var audQ = BuildAuditQuery(query);
            long total = await audQ.LongCountAsync(ct);
            var rows = await audQ
                .OrderByDescending(a => a.TimestampUtc)
                .ThenByDescending(a => a.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            var txIds = rows.Where(r => !string.IsNullOrEmpty(r.TransactionId)).Select(r => r.TransactionId!).Distinct().ToList();
            var matchingCdc = txIds.Count > 0
                ? await _db.CdcEvents.AsNoTracking().Where(c => txIds.Contains(c.TransactionId)).ToListAsync(ct)
                : new List<CdcEvent>();

            var items = rows.Select(a =>
            {
                var wal = matchingCdc.FirstOrDefault(c =>
                    c.TransactionId == a.TransactionId &&
                    string.Equals(c.TableName, a.EntityName, StringComparison.OrdinalIgnoreCase));
                return MapAuditToDto(a, wal, societeNames);
            }).ToList();

            return new PagedRedoLogsResponseDto
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Items = items
            };
        }

        // Source = ALL : fusion paginée propre via projection union / SQL ou double fenêtre bornée
        var auditQ = BuildAuditQuery(query);
        var cdcWalQ = BuildCdcQuery(query);

        long totalAuditCount = includeAudit ? await auditQ.LongCountAsync(ct) : 0;
        long totalCdcCount = includeCdc ? await cdcWalQ.LongCountAsync(ct) : 0;

        // Pour éviter un chargement mémoire illimité, on borne l'extraction à (skip + pageSize) de chaque source
        int fetchLimit = skip + pageSize;

        var topAudits = includeAudit
            ? await auditQ.OrderByDescending(a => a.TimestampUtc).ThenByDescending(a => a.Id).Take(fetchLimit).ToListAsync(ct)
            : new List<AuditTrailEntry>();

        var topCdc = includeCdc
            ? await cdcWalQ.OrderByDescending(c => c.CommitTimestampUtc).ThenByDescending(c => c.Id).Take(fetchLimit).ToListAsync(ct)
            : new List<CdcEvent>();

        var combinedTxIds = topAudits.Where(a => a.TransactionId != null).Select(a => a.TransactionId!)
            .Union(topCdc.Select(c => c.TransactionId))
            .Distinct()
            .ToList();

        var walByTx = topCdc.GroupBy(c => c.TransactionId).ToDictionary(g => g.Key, g => g.ToList());
        var auditByTx = topAudits.Where(a => a.TransactionId != null).GroupBy(a => a.TransactionId!).ToDictionary(g => g.Key, g => g.ToList());

        var unified = new List<RedoLogRecordDto>();

        foreach (var a in topAudits)
        {
            CdcEvent? wal = null;
            if (a.TransactionId != null && walByTx.TryGetValue(a.TransactionId, out var cands))
            {
                wal = cands.FirstOrDefault(c => string.Equals(c.TableName, a.EntityName, StringComparison.OrdinalIgnoreCase));
            }
            unified.Add(MapAuditToDto(a, wal, societeNames));
        }

        foreach (var c in topCdc)
        {
            AuditTrailEntry? linked = null;
            if (auditByTx.TryGetValue(c.TransactionId, out var aCands))
            {
                linked = aCands.FirstOrDefault(a =>
                    string.Equals(a.EntityName, c.TableName, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(c.RecordId) || a.EntityId == c.RecordId));
            }
            unified.Add(MapCdcToDto(c, linked, societeNames));
        }

        var totalAll = totalAuditCount + totalCdcCount;
        var pageItems = unified
            .OrderByDescending(x => x.TimestampUtc)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return new PagedRedoLogsResponseDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalAll,
            TotalPages = (int)Math.Ceiling(totalAll / (double)pageSize),
            Items = pageItems
        };
    }

    private IQueryable<AuditTrailEntry> BuildAuditQuery(RedoLogQueryParams q)
    {
        var query = _db.AuditTrailEntries.AsNoTracking().AsQueryable();

        if (q.FromDate.HasValue)
            query = query.Where(a => a.TimestampUtc >= DateTime.SpecifyKind(q.FromDate.Value, DateTimeKind.Utc));
        if (q.ToDate.HasValue)
            query = query.Where(a => a.TimestampUtc <= DateTime.SpecifyKind(q.ToDate.Value, DateTimeKind.Utc));
        if (q.SocieteId.HasValue && q.SocieteId.Value > 0)
            query = query.Where(a => a.SocieteId == q.SocieteId.Value);
        if (!string.IsNullOrWhiteSpace(q.TableName) && q.TableName != "ALL")
            query = query.Where(a => a.EntityName == q.TableName);
        if (!string.IsNullOrWhiteSpace(q.Operation) && q.Operation != "ALL")
        {
            var op = q.Operation.ToUpperInvariant();
            if (op == "INSERT")
                query = query.Where(a => a.Action == "CREATE" || a.Action == "INSERT");
            else
                query = query.Where(a => a.Action == op);
        }
        if (q.UserId.HasValue && q.UserId.Value > 0)
            query = query.Where(a => a.UserId == q.UserId.Value);
        if (!string.IsNullOrWhiteSpace(q.UserSearch))
        {
            var us = q.UserSearch.ToLower();
            query = query.Where(a => a.UserName != null && a.UserName.ToLower().Contains(us));
        }
        if (!string.IsNullOrWhiteSpace(q.TransactionId))
            query = query.Where(a => a.TransactionId != null && a.TransactionId.Contains(q.TransactionId));
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.ToLower();
            query = query.Where(a =>
                a.EntityName.ToLower().Contains(s) ||
                a.EntityId.ToLower().Contains(s) ||
                a.BusinessActionLabel.ToLower().Contains(s) ||
                (a.UserName != null && a.UserName.ToLower().Contains(s)) ||
                (a.TransactionId != null && a.TransactionId.ToLower().Contains(s)) ||
                (a.OldValuesJson != null && a.OldValuesJson.ToLower().Contains(s)) ||
                (a.NewValuesJson != null && a.NewValuesJson.ToLower().Contains(s)));
        }

        return query;
    }

    private IQueryable<CdcEvent> BuildCdcQuery(RedoLogQueryParams q)
    {
        var query = _db.CdcEvents.AsNoTracking().AsQueryable();

        if (q.FromDate.HasValue)
            query = query.Where(c => c.CommitTimestampUtc >= DateTime.SpecifyKind(q.FromDate.Value, DateTimeKind.Utc));
        if (q.ToDate.HasValue)
            query = query.Where(c => c.CommitTimestampUtc <= DateTime.SpecifyKind(q.ToDate.Value, DateTimeKind.Utc));
        if (q.SocieteId.HasValue && q.SocieteId.Value > 0)
            query = query.Where(c => c.SocieteId == q.SocieteId.Value);
        if (!string.IsNullOrWhiteSpace(q.TableName) && q.TableName != "ALL")
            query = query.Where(c => c.TableName == q.TableName);
        if (!string.IsNullOrWhiteSpace(q.Operation) && q.Operation != "ALL")
        {
            var op = q.Operation.ToUpperInvariant();
            if (op == "CREATE") op = "INSERT";
            query = query.Where(c => c.Operation == op);
        }
        if (q.UserId.HasValue && q.UserId.Value > 0)
        {
            // Filtrer via txIds associés à l'utilisateur si connu dans l'Audit Trail
            var userTx = _db.AuditTrailEntries.AsNoTracking()
                .Where(a => a.UserId == q.UserId.Value && a.TransactionId != null)
                .Select(a => a.TransactionId!);
            query = query.Where(c => userTx.Contains(c.TransactionId));
        }
        if (!string.IsNullOrWhiteSpace(q.TransactionId))
            query = query.Where(c => c.TransactionId.Contains(q.TransactionId));
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.ToLower();
            query = query.Where(c =>
                c.TableName.ToLower().Contains(s) ||
                (c.RecordId != null && c.RecordId.ToLower().Contains(s)) ||
                c.Lsn.ToLower().Contains(s) ||
                c.TransactionId.ToLower().Contains(s) ||
                (c.OldValuesJson != null && c.OldValuesJson.ToLower().Contains(s)) ||
                (c.NewValuesJson != null && c.NewValuesJson.ToLower().Contains(s)));
        }

        return query;
    }

    private RedoLogRecordDto MapAuditToDto(AuditTrailEntry a, CdcEvent? correlatedWal, Dictionary<int, string> societeNames)
    {
        bool canCompensate = _compensationService.IsEntryCompensable(
            a.EntityName, a.Action, a.ChangedProperties, a.IsCompensated, out var note);

        string socName = a.SocieteName ?? (a.SocieteId.HasValue && societeNames.TryGetValue(a.SocieteId.Value, out var sn)
            ? sn
            : (a.SocieteId.HasValue ? $"Société #{a.SocieteId}" : "Plateforme Centrale"));

        return new RedoLogRecordDto
        {
            EventKey = $"AUD-{a.Id}",
            NumericId = a.Id,
            Source = a.Source,
            Operation = a.Action,
            TableName = a.EntityName,
            RecordId = a.EntityId,
            BusinessLabel = a.BusinessActionLabel,
            SocieteId = a.SocieteId,
            SocieteName = socName,
            UserId = a.UserId,
            UserName = a.UserName,
            UserRole = a.UserRole,
            TimestampUtc = a.TimestampUtc,
            TransactionId = a.TransactionId,
            Lsn = correlatedWal?.Lsn,
            CorrelationId = a.CorrelationId,
            OldValuesJson = a.OldValuesJson,
            NewValuesJson = a.NewValuesJson,
            ChangedProperties = a.ChangedProperties,
            IsCompensated = a.IsCompensated,
            CanCompensate = canCompensate,
            CompensationNote = note,
            HasWalCorrelation = correlatedWal != null
        };
    }

    private static RedoLogRecordDto MapCdcToDto(CdcEvent c, AuditTrailEntry? linkedAudit, Dictionary<int, string> societeNames)
    {
        string socName = c.SocieteId.HasValue && societeNames.TryGetValue(c.SocieteId.Value, out var sn)
            ? sn
            : (c.SocieteId.HasValue ? $"Société #{c.SocieteId}" : "PostgreSQL Direct");

        return new RedoLogRecordDto
        {
            EventKey = $"WAL-{c.Id}",
            NumericId = c.Id,
            Source = c.Source,
            Operation = c.Operation,
            TableName = c.TableName,
            RecordId = c.RecordId ?? "-",
            BusinessLabel = $"[WAL pgoutput] {c.Operation} sur {c.SchemaName}.{c.TableName}",
            SocieteId = c.SocieteId,
            SocieteName = socName,
            UserId = linkedAudit?.UserId,
            UserName = linkedAudit?.UserName ?? "PostgreSQL WAL Stream",
            UserRole = linkedAudit?.UserRole ?? "pgoutput",
            TimestampUtc = c.CommitTimestampUtc,
            TransactionId = c.TransactionId,
            Lsn = c.Lsn,
            CorrelationId = linkedAudit?.CorrelationId,
            OldValuesJson = c.OldValuesJson,
            NewValuesJson = c.NewValuesJson,
            ChangedProperties = null,
            IsCompensated = false,
            CanCompensate = false,
            CompensationNote = "Événement physique WAL : la compensation métier s'applique uniquement depuis la ligne Audit Trail correspondante.",
            HasWalCorrelation = true
        };
    }
}
