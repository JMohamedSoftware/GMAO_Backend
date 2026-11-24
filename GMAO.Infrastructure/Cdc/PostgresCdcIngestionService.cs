using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;
using GMAO.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using NpgsqlTypes;

namespace GMAO.Infrastructure.Cdc;

/// <summary>
/// Moteur réel de consommation du Write-Ahead Log (WAL) PostgreSQL via Logical Decoding (pgoutput).
/// - Ne fabrique ni ne simule JAMAIS de données WAL ou de LSN.
/// - Sondage préalable réel des capacités du serveur PostgreSQL (wal_level, max_replication_slots, permissions, réseau).
/// - Tamponne les messages par transaction (Begin -> Relation/Insert/Update/Delete -> Commit),
///   applique le masquage des colonnes sensibles (AuditSanitizer), déduplique les événements par LSN,
///   persiste en base avec checkpoint atomique, puis accuse réception auprès de PostgreSQL (SetReplicationStatus).
/// </summary>
public class PostgresCdcIngestionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PostgresCdcOptions> _options;
    private readonly ILogger<PostgresCdcIngestionService> _logger;

    private readonly Dictionary<uint, CachedRelation> _relations = new();

    public PostgresCdcIngestionService(
        IServiceScopeFactory scopeFactory,
        IOptions<PostgresCdcOptions> options,
        ILogger<PostgresCdcIngestionService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<CdcWorkerState> ProbeCapabilitiesAsync(CancellationToken ct = default)
    {
        var opts = _options.Value;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GmaoDbContext>();

        var state = await GetOrCreateStateAsync(db, opts, ct);
        state.PublicationName = opts.PublicationName;
        state.LastHeartbeatUtc = DateTime.UtcNow;
        state.UpdatedAtUtc = DateTime.UtcNow;

        if (!opts.Enabled)
        {
            state.Status = "Disabled";
            state.StatusReason = "Streaming WAL / Logical Decoding désactivé (CdcSettings:Enabled=false). Seul l'Audit Trail applicatif est actif.";
            await db.SaveChangesAsync(ct);
            return state;
        }

        if (string.IsNullOrWhiteSpace(opts.ReplicationConnectionString) ||
            opts.ReplicationConnectionString.Contains("REPLACED_BY_ENV_VAR", StringComparison.OrdinalIgnoreCase))
        {
            state.Status = "NotConfigured";
            state.StatusReason = "Chaîne de connexion directe PostgreSQL non configurée dans CdcSettings:ReplicationConnectionString (un pooler Supavisor ne supporte pas le protocole de réplication logique).";
            await db.SaveChangesAsync(ct);
            return state;
        }

        try
        {
            await using var conn = new NpgsqlConnection(opts.ReplicationConnectionString);
            await conn.OpenAsync(ct);

            await using (var cmd = new NpgsqlCommand("SELECT version(), current_setting('wal_level'), current_setting('max_replication_slots')::int, current_setting('max_wal_senders')::int, pg_current_wal_lsn()::text", conn))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                if (await reader.ReadAsync(ct))
                {
                    state.PostgresVersion = reader.IsDBNull(0) ? null : reader.GetString(0);
                    state.WalLevel = reader.IsDBNull(1) ? null : reader.GetString(1);
                    state.MaxReplicationSlots = reader.IsDBNull(2) ? null : reader.GetInt32(2);
                    state.MaxWalSenders = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                    state.CurrentServerWalLsn = reader.IsDBNull(4) ? null : reader.GetString(4);
                }
            }

            bool hasReplicationPriv = false;
            await using (var roleCmd = new NpgsqlCommand("SELECT rolsuper, rolreplication FROM pg_roles WHERE rolname = current_user", conn))
            await using (var roleReader = await roleCmd.ExecuteReaderAsync(ct))
            {
                if (await roleReader.ReadAsync(ct))
                {
                    bool isSuper = roleReader.GetBoolean(0);
                    bool isRepl = roleReader.GetBoolean(1);
                    hasReplicationPriv = isSuper || isRepl;
                }
            }

            if (!string.Equals(state.WalLevel, "logical", StringComparison.OrdinalIgnoreCase))
            {
                state.Status = "IncompatibleWalLevel";
                state.StatusReason = $"Le serveur PostgreSQL a wal_level='{state.WalLevel}'. Le décodage logique pgoutput exige wal_level='logical'.";
                await db.SaveChangesAsync(ct);
                return state;
            }

            if (!hasReplicationPriv)
            {
                state.Status = "PermissionDenied";
                state.StatusReason = "Le compte PostgreSQL configuré ne dispose ni du rôle REPLICATION ni des privilèges superuser nécessaires pour consommer un slot de réplication.";
                await db.SaveChangesAsync(ct);
                return state;
            }

            // Lire les métriques réelles du slot s'il existe déjà
            await RefreshSlotMetricsAsync(conn, state, opts.SlotName, ct);

            state.Status = state.Status == "ActiveStreaming" ? "ActiveStreaming" : "Ready";
            state.StatusReason = "Capacités PostgreSQL Logical Decoding vérifiées (wal_level=logical, privilège REPLICATION présent).";
            await db.SaveChangesAsync(ct);
            return state;
        }
        catch (SocketException sockEx)
        {
            state.Status = "NetworkBlocked";
            state.StatusReason = $"Connexion directe PostgreSQL impossible ({sockEx.SocketErrorCode}) : {sockEx.Message}. Sur Supabase sans Add-on IPv4, l'hôte direct résout uniquement en IPv6 (AAAA), incompatible avec un conteneur IPv4-only comme Render.";
            await db.SaveChangesAsync(ct);
            return state;
        }
        catch (TimeoutException timeEx)
        {
            state.Status = "NetworkBlocked";
            state.StatusReason = $"Délai d'attente dépassé lors de la connexion directe PostgreSQL : {timeEx.Message}";
            await db.SaveChangesAsync(ct);
            return state;
        }
        catch (NpgsqlException npgEx)
        {
            var isNetwork = npgEx.InnerException is SocketException
                || npgEx.InnerException is TimeoutException
                || npgEx.GetBaseException() is SocketException
                || (npgEx.InnerException is AggregateException agg && agg.InnerExceptions.Any(e => e is SocketException || e is TimeoutException));
            state.Status = isNetwork ? "NetworkBlocked" : "Error";
            state.StatusReason = $"Erreur PostgreSQL lors du contrôle WAL : {npgEx.Message}";
            await db.SaveChangesAsync(ct);
            return state;
        }
        catch (Exception ex)
        {
            state.Status = "Error";
            state.StatusReason = $"Erreur lors du contrôle des capacités WAL : {ex.Message}";
            await db.SaveChangesAsync(ct);
            return state;
        }
    }

    public async Task RunStreamingSessionAsync(CancellationToken ct)
    {
        var opts = _options.Value;
        var probed = await ProbeCapabilitiesAsync(ct);
        if (probed.Status != "Ready" && probed.Status != "ActiveStreaming")
        {
            _logger.LogInformation("Session streaming CDC ignorée : {Status} — {Reason}", probed.Status, probed.StatusReason);
            return;
        }

        var connStr = opts.ReplicationConnectionString!;
        await EnsurePublicationAndSlotAsync(connStr, opts, ct);

        NpgsqlLogSequenceNumber? startLsn = null;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var st = await GetOrCreateStateAsync(db, opts, ct);
            if (!string.IsNullOrWhiteSpace(st.LastConsumedLsn) && NpgsqlLogSequenceNumber.TryParse(st.LastConsumedLsn, out var parsedLsn))
            {
                startLsn = parsedLsn;
            }
            st.Status = "ActiveStreaming";
            st.StatusReason = $"Streaming actif depuis le slot '{opts.SlotName}' (publication '{opts.PublicationName}').";
            st.SlotActive = true;
            st.LastHeartbeatUtc = DateTime.UtcNow;
            st.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        await using var replConn = new LogicalReplicationConnection(connStr);
        await replConn.Open(ct);

        var slot = new PgOutputReplicationSlot(opts.SlotName);
        var pgOutputOptions = new PgOutputReplicationOptions(opts.PublicationName, protocolVersion: 1, binary: false, streaming: false);

        PendingWalTransaction? currentTx = null;

        try
        {
            IAsyncEnumerable<PgOutputReplicationMessage> stream = startLsn.HasValue
                ? replConn.StartReplication(slot, pgOutputOptions, ct, startLsn.Value)
                : replConn.StartReplication(slot, pgOutputOptions, ct);

            await foreach (var message in stream.WithCancellation(ct))
            {
                switch (message)
                {
                    case BeginMessage begin:
                        currentTx = new PendingWalTransaction
                        {
                            Xid = begin.TransactionXid.ToString(),
                            CommitTimestampUtc = DateTime.SpecifyKind(begin.TransactionCommitTimestamp, DateTimeKind.Utc),
                            FinalLsn = begin.TransactionFinalLsn.ToString()
                        };
                        break;

                    case RelationMessage rel:
                        _relations[rel.RelationId] = new CachedRelation
                        {
                            Schema = rel.Namespace,
                            Table = rel.RelationName,
                            Columns = rel.Columns.Select(c => new CachedColumn
                            {
                                Name = c.ColumnName,
                                IsKey = (c.Flags & RelationMessage.Column.ColumnFlags.PartOfKey) != 0
                            }).ToList()
                        };
                        break;

                    case InsertMessage insert when currentTx != null && _relations.TryGetValue(insert.Relation.RelationId, out var insRel):
                        if (!AuditSanitizer.IsExcludedTable(insRel.Table))
                        {
                            var newVals = await ReadTupleValuesAsync(insert.NewRow, insRel, ct);
                            currentTx.Events.Add(BuildCdcEvent("INSERT", insRel, null, newVals, currentTx, insert.WalEnd, opts));
                        }
                        break;

                    case FullUpdateMessage fullUpd when currentTx != null && _relations.TryGetValue(fullUpd.Relation.RelationId, out var fullRel):
                        if (!AuditSanitizer.IsExcludedTable(fullRel.Table))
                        {
                            var oldVals = await ReadTupleValuesAsync(fullUpd.OldRow, fullRel, ct);
                            var newVals = await ReadTupleValuesAsync(fullUpd.NewRow, fullRel, ct);
                            currentTx.Events.Add(BuildCdcEvent("UPDATE", fullRel, oldVals, newVals, currentTx, fullUpd.WalEnd, opts));
                        }
                        break;

                    case IndexUpdateMessage idxUpd when currentTx != null && _relations.TryGetValue(idxUpd.Relation.RelationId, out var idxRel):
                        if (!AuditSanitizer.IsExcludedTable(idxRel.Table))
                        {
                            var oldKey = await ReadTupleValuesAsync(idxUpd.Key, idxRel, ct);
                            var newVals = await ReadTupleValuesAsync(idxUpd.NewRow, idxRel, ct);
                            currentTx.Events.Add(BuildCdcEvent("UPDATE", idxRel, oldKey, newVals, currentTx, idxUpd.WalEnd, opts));
                        }
                        break;

                    case DefaultUpdateMessage defUpd when currentTx != null && _relations.TryGetValue(defUpd.Relation.RelationId, out var defRel):
                        if (!AuditSanitizer.IsExcludedTable(defRel.Table))
                        {
                            var newVals = await ReadTupleValuesAsync(defUpd.NewRow, defRel, ct);
                            currentTx.Events.Add(BuildCdcEvent("UPDATE", defRel, null, newVals, currentTx, defUpd.WalEnd, opts));
                        }
                        break;

                    case FullDeleteMessage fullDel when currentTx != null && _relations.TryGetValue(fullDel.Relation.RelationId, out var fDelRel):
                        if (!AuditSanitizer.IsExcludedTable(fDelRel.Table))
                        {
                            var oldVals = await ReadTupleValuesAsync(fullDel.OldRow, fDelRel, ct);
                            currentTx.Events.Add(BuildCdcEvent("DELETE", fDelRel, oldVals, null, currentTx, fullDel.WalEnd, opts));
                        }
                        break;

                    case KeyDeleteMessage keyDel when currentTx != null && _relations.TryGetValue(keyDel.Relation.RelationId, out var kDelRel):
                        if (!AuditSanitizer.IsExcludedTable(kDelRel.Table))
                        {
                            var oldKey = await ReadTupleValuesAsync(keyDel.Key, kDelRel, ct);
                            currentTx.Events.Add(BuildCdcEvent("DELETE", kDelRel, oldKey, null, currentTx, keyDel.WalEnd, opts));
                        }
                        break;

                    case CommitMessage commit:
                        await PersistTransactionEventsAsync(currentTx, commit.TransactionEndLsn, opts, ct);
                        currentTx = null;

                        // N'accuser réception auprès de PostgreSQL QU'APRÈS commit durable du checkpoint et des événements
                        replConn.SetReplicationStatus(commit.TransactionEndLsn);
                        await replConn.SendStatusUpdate(ct);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await MarkWorkerStoppedAsync(opts, "Stopped", "Arrêt propre du Worker CDC.", CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await MarkWorkerStoppedAsync(opts, "Error", $"Flux WAL interrompu : {ex.Message}", CancellationToken.None);
            throw;
        }
    }

    private async Task MarkWorkerStoppedAsync(PostgresCdcOptions opts, string status, string reason, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var st = await GetOrCreateStateAsync(db, opts, ct);
            st.Status = status;
            st.StatusReason = reason;
            st.SlotActive = false;
            st.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Ignorer en cas de DB indisponible pendant l'arrêt
        }
    }

    private async Task EnsurePublicationAndSlotAsync(string connStr, PostgresCdcOptions opts, CancellationToken ct)
    {
        await using (var stdConn = new NpgsqlConnection(connStr))
        {
            await stdConn.OpenAsync(ct);

            await using var checkPub = new NpgsqlCommand("SELECT 1 FROM pg_publication WHERE pubname = @pub", stdConn);
            checkPub.Parameters.AddWithValue("pub", opts.PublicationName);
            var exists = await checkPub.ExecuteScalarAsync(ct);
            if (exists == null)
            {
                await using var createPub = new NpgsqlCommand($"CREATE PUBLICATION \"{opts.PublicationName.Replace("\"", "")}\" FOR ALL TABLES", stdConn);
                await createPub.ExecuteNonQueryAsync(ct);
            }

            // S'assurer que le slot existe avant StartReplication
            await using var checkSlot = new NpgsqlCommand("SELECT 1 FROM pg_replication_slots WHERE slot_name = @slot", stdConn);
            checkSlot.Parameters.AddWithValue("slot", opts.SlotName);
            var slotExists = await checkSlot.ExecuteScalarAsync(ct);
            if (slotExists == null)
            {
                await using var createSlot = new NpgsqlCommand("SELECT pg_create_logical_replication_slot(@slot, 'pgoutput')", stdConn);
                createSlot.Parameters.AddWithValue("slot", opts.SlotName);
                await createSlot.ExecuteNonQueryAsync(ct);
            }
        }
    }

    private async Task PersistTransactionEventsAsync(
        PendingWalTransaction? tx,
        NpgsqlLogSequenceNumber endLsn,
        PostgresCdcOptions opts,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GmaoDbContext>();
        var lsnString = endLsn.ToString();

        var state = await GetOrCreateStateAsync(db, opts, ct);

        if (tx != null && tx.Events.Count > 0)
        {
            var eventsToInsert = new List<CdcEvent>();
            foreach (var ev in tx.Events)
            {
                ev.Lsn = lsnString;
                // Idempotence / Déduplication lors d'une reprise depuis un LSN antérieur
                bool alreadyExists = await db.CdcEvents.AnyAsync(c =>
                    c.Lsn == ev.Lsn &&
                    c.TableName == ev.TableName &&
                    c.Operation == ev.Operation &&
                    c.RecordId == ev.RecordId, ct);

                if (!alreadyExists)
                {
                    eventsToInsert.Add(ev);
                }
            }

            if (eventsToInsert.Count > 0)
            {
                await db.CdcEvents.AddRangeAsync(eventsToInsert, ct);
                state.TotalEventsProcessed += eventsToInsert.Count;
                state.LastEventAtUtc = DateTime.UtcNow;
            }
        }

        state.LastConsumedLsn = lsnString;
        state.LastHeartbeatUtc = DateTime.UtcNow;
        state.UpdatedAtUtc = DateTime.UtcNow;
        state.Status = "ActiveStreaming";

        await db.SaveChangesAsync(ct);
    }

    private static async Task<Dictionary<string, object?>> ReadTupleValuesAsync(
        ReplicationTuple tuple,
        CachedRelation rel,
        CancellationToken ct)
    {
        var dict = new Dictionary<string, object?>();
        int idx = 0;

        await foreach (var value in tuple.WithCancellation(ct))
        {
            string colName = idx < rel.Columns.Count ? rel.Columns[idx].Name : $"col_{idx}";
            idx++;

            if (value.IsDBNull)
            {
                dict[colName] = null;
            }
            else if (value.IsUnchangedToastedValue)
            {
                dict[colName] = "[unchanged_toast]";
            }
            else
            {
                dict[colName] = await value.Get<string>(ct);
            }
        }

        return dict;
    }

    private static CdcEvent BuildCdcEvent(
        string op,
        CachedRelation rel,
        Dictionary<string, object?>? oldVals,
        Dictionary<string, object?>? newVals,
        PendingWalTransaction tx,
        NpgsqlLogSequenceNumber walEnd,
        PostgresCdcOptions opts)
    {
        var activeMap = newVals ?? oldVals ?? new Dictionary<string, object?>();
        string? recordId = null;

        var keyCol = rel.Columns.FirstOrDefault(c => c.IsKey)?.Name ?? "Id";
        if (activeMap.TryGetValue(keyCol, out var pk) && pk != null)
            recordId = pk.ToString();
        else if (activeMap.TryGetValue("Id", out var idVal) && idVal != null)
            recordId = idVal.ToString();

        int? societeId = null;
        if (activeMap.TryGetValue("SocieteId", out var socVal) && socVal != null && int.TryParse(socVal.ToString(), out int parsedSid) && parsedSid > 0)
            societeId = parsedSid;
        else if (string.Equals(rel.Table, "Societes", StringComparison.OrdinalIgnoreCase) && int.TryParse(recordId, out int socSelfId) && socSelfId > 0)
            societeId = socSelfId;

        return new CdcEvent
        {
            Operation = op,
            SchemaName = rel.Schema,
            TableName = rel.Table,
            RecordId = recordId,
            SocieteId = societeId,
            TransactionId = tx.Xid,
            Lsn = string.IsNullOrEmpty(tx.FinalLsn) ? walEnd.ToString() : tx.FinalLsn,
            CommitTimestampUtc = tx.CommitTimestampUtc,
            ReceivedAtUtc = DateTime.UtcNow,
            OldValuesJson = AuditSanitizer.SerializeSanitized(oldVals),
            NewValuesJson = AuditSanitizer.SerializeSanitized(newVals),
            SlotName = opts.SlotName,
            PublicationName = opts.PublicationName,
            Source = "PostgreSQL_WAL_pgoutput"
        };
    }

    private static async Task RefreshSlotMetricsAsync(NpgsqlConnection conn, CdcWorkerState state, string slotName, CancellationToken ct)
    {
        const string sql = @"
            SELECT active,
                   wal_status,
                   confirmed_flush_lsn::text,
                   pg_wal_lsn_diff(pg_current_wal_lsn(), restart_lsn)::bigint AS retained_bytes,
                   pg_wal_lsn_diff(pg_current_wal_lsn(), confirmed_flush_lsn)::bigint AS lag_bytes
            FROM pg_replication_slots
            WHERE slot_name = @slot";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("slot", slotName);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (await r.ReadAsync(ct))
        {
            state.SlotActive = r.IsDBNull(0) ? null : r.GetBoolean(0);
            state.WalStatus = r.IsDBNull(1) ? null : r.GetString(1);
            if (!r.IsDBNull(2) && string.IsNullOrWhiteSpace(state.LastConsumedLsn))
                state.LastConsumedLsn = r.GetString(2);
            state.RetainedWalBytes = r.IsDBNull(3) ? null : r.GetInt64(3);
            state.ReplicationLagBytes = r.IsDBNull(4) ? null : r.GetInt64(4);
        }
    }

    private static async Task<CdcWorkerState> GetOrCreateStateAsync(GmaoDbContext db, PostgresCdcOptions opts, CancellationToken ct)
    {
        var state = await db.CdcWorkerStates.FirstOrDefaultAsync(w => w.SlotName == opts.SlotName, ct);
        if (state == null)
        {
            state = new CdcWorkerState
            {
                SlotName = opts.SlotName,
                PublicationName = opts.PublicationName
            };
            db.CdcWorkerStates.Add(state);
        }
        return state;
    }

    private sealed class CachedRelation
    {
        public string Schema { get; set; } = "public";
        public string Table { get; set; } = string.Empty;
        public List<CachedColumn> Columns { get; set; } = new();
    }

    private sealed class CachedColumn
    {
        public string Name { get; set; } = string.Empty;
        public bool IsKey { get; set; }
    }

    private sealed class PendingWalTransaction
    {
        public string Xid { get; set; } = string.Empty;
        public DateTime CommitTimestampUtc { get; set; }
        public string FinalLsn { get; set; } = string.Empty;
        public List<CdcEvent> Events { get; set; } = new();
    }
}
