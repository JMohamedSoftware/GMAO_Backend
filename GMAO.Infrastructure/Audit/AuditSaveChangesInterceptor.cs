using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace GMAO.Infrastructure.Audit;

/// <summary>
/// Intercepteur EF Core transactionnel garantissant un Audit Trail applicatif fidèle et atomique :
/// - Enregistre l'acteur (UserId, UserName, UserRole), l'entreprise (SocieteId), le CorrelationId HTTP,
///   le véritable identifiant de transaction PostgreSQL (pg_current_xact_id::xid), et les diffs masqués avant/après.
/// - Garantit l'atomicité transactionnelle : en cas d'échec ou d'annulation, aucune trace d'audit erronée n'est persistée.
/// - Exclut formellement secrets, mots de passe, hashes BCrypt et jetons JWT/RefreshToken.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<AuditSaveChangesInterceptor>? _logger;

    private static readonly AsyncLocal<bool> IsPersistingAudit = new();
    private readonly ConditionalWeakTable<DbContext, PendingAuditBatch> _batches = new();

    public AuditSaveChangesInterceptor(
        IHttpContextAccessor? httpContextAccessor = null,
        ILogger<AuditSaveChangesInterceptor>? logger = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (IsPersistingAudit.Value || eventData.Context == null)
            return base.SavingChanges(eventData, result);

        PrepareAuditBatch(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (IsPersistingAudit.Value || eventData.Context == null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        await PrepareAuditBatchAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (IsPersistingAudit.Value || eventData.Context == null)
            return base.SavedChanges(eventData, result);

        FlushAuditBatch(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (IsPersistingAudit.Value || eventData.Context == null)
            return await base.SavedChangesAsync(eventData, result, cancellationToken);

        await FlushAuditBatchAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context != null && !IsPersistingAudit.Value)
        {
            AbortAuditBatch(eventData.Context);
        }
        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null && !IsPersistingAudit.Value)
        {
            await AbortAuditBatchAsync(eventData.Context, cancellationToken);
        }
        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void PrepareAuditBatch(DbContext context)
    {
        var items = CapturePendingItems(context);
        if (items.Count == 0) return;

        var batch = new PendingAuditBatch { Items = items };

        if (context.Database.IsRelational())
        {
            if (context.Database.CurrentTransaction == null)
            {
                batch.OwnedTransaction = context.Database.BeginTransaction();
            }
            batch.TransactionId = TryReadPostgresXid(context);
        }

        _batches.AddOrUpdate(context, batch);
    }

    private async Task PrepareAuditBatchAsync(DbContext context, CancellationToken ct)
    {
        var items = CapturePendingItems(context);
        if (items.Count == 0) return;

        var batch = new PendingAuditBatch { Items = items };

        if (context.Database.IsRelational())
        {
            if (context.Database.CurrentTransaction == null)
            {
                batch.OwnedTransaction = await context.Database.BeginTransactionAsync(ct);
            }
            batch.TransactionId = await TryReadPostgresXidAsync(context, ct);
        }

        _batches.AddOrUpdate(context, batch);
    }

    private void FlushAuditBatch(DbContext context)
    {
        if (!_batches.TryGetValue(context, out var batch)) return;
        _batches.Remove(context);

        if (batch.Items.Count == 0)
        {
            batch.OwnedTransaction?.Commit();
            batch.OwnedTransaction?.Dispose();
            return;
        }

        try
        {
            var entities = BuildAuditEntities(batch);
            if (entities.Count > 0)
            {
                IsPersistingAudit.Value = true;
                context.Set<AuditTrailEntry>().AddRange(entities);
                context.SaveChanges();
            }

            batch.OwnedTransaction?.Commit();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Échec lors de la persistance de l'Audit Trail applicatif.");
            try { batch.OwnedTransaction?.Rollback(); } catch { }
            throw;
        }
        finally
        {
            IsPersistingAudit.Value = false;
            batch.OwnedTransaction?.Dispose();
        }
    }

    private async Task FlushAuditBatchAsync(DbContext context, CancellationToken ct)
    {
        if (!_batches.TryGetValue(context, out var batch)) return;
        _batches.Remove(context);

        if (batch.Items.Count == 0)
        {
            if (batch.OwnedTransaction != null)
            {
                await batch.OwnedTransaction.CommitAsync(ct);
                await batch.OwnedTransaction.DisposeAsync();
            }
            return;
        }

        try
        {
            var entities = BuildAuditEntities(batch);
            if (entities.Count > 0)
            {
                IsPersistingAudit.Value = true;
                context.Set<AuditTrailEntry>().AddRange(entities);
                await context.SaveChangesAsync(ct);
            }

            if (batch.OwnedTransaction != null)
            {
                await batch.OwnedTransaction.CommitAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Échec asynchrone lors de la persistance de l'Audit Trail applicatif.");
            if (batch.OwnedTransaction != null)
            {
                try { await batch.OwnedTransaction.RollbackAsync(ct); } catch { }
            }
            throw;
        }
        finally
        {
            IsPersistingAudit.Value = false;
            if (batch.OwnedTransaction != null)
            {
                await batch.OwnedTransaction.DisposeAsync();
            }
        }
    }

    private void AbortAuditBatch(DbContext context)
    {
        if (!_batches.TryGetValue(context, out var batch)) return;
        _batches.Remove(context);
        try { batch.OwnedTransaction?.Rollback(); } catch { }
        batch.OwnedTransaction?.Dispose();
    }

    private async Task AbortAuditBatchAsync(DbContext context, CancellationToken ct)
    {
        if (!_batches.TryGetValue(context, out var batch)) return;
        _batches.Remove(context);
        if (batch.OwnedTransaction != null)
        {
            try { await batch.OwnedTransaction.RollbackAsync(ct); } catch { }
            await batch.OwnedTransaction.DisposeAsync();
        }
    }

    private List<PendingAuditItem> CapturePendingItems(DbContext context)
    {
        var items = new List<PendingAuditItem>();
        var actor = ExtractActorMetadata();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            if (entry.Entity is AuditTrailEntry or CdcEvent or CdcWorkerState or JournalSuperAdmin or RefreshToken)
                continue;

            var tableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;
            if (AuditSanitizer.IsExcludedTable(tableName))
                continue;

            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();
            var changedCols = new List<string>();
            var action = entry.State switch
            {
                EntityState.Added => "CREATE",
                EntityState.Deleted => "DELETE",
                _ => "UPDATE"
            };

            if (entry.State == EntityState.Added)
            {
                foreach (var prop in entry.Properties)
                {
                    if (prop.IsTemporary) continue;
                    newValues[prop.Metadata.Name] = prop.CurrentValue;
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                foreach (var prop in entry.Properties)
                {
                    oldValues[prop.Metadata.Name] = prop.OriginalValue;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                foreach (var prop in entry.Properties)
                {
                    if (!prop.IsModified) continue;
                    if (Equals(prop.OriginalValue, prop.CurrentValue)) continue;

                    oldValues[prop.Metadata.Name] = prop.OriginalValue;
                    newValues[prop.Metadata.Name] = prop.CurrentValue;
                    changedCols.Add(prop.Metadata.Name);
                }

                if (changedCols.Count == 0)
                    continue;

                if (tableName == "OrdresTravail" && changedCols.Contains("Statut"))
                {
                    action = "STATUS_CHANGE";
                }
            }

            int? entitySocieteId = TryExtractSocieteId(entry) ?? actor.SocieteId;
            var label = BuildBusinessLabel(tableName, action, changedCols);

            items.Add(new PendingAuditItem
            {
                Entry = entry,
                TableName = tableName,
                Action = action,
                BusinessActionLabel = label,
                EntitySocieteId = entitySocieteId,
                UserId = actor.UserId,
                UserName = actor.UserName,
                UserRole = actor.UserRole,
                CorrelationId = actor.CorrelationId,
                IpAddress = actor.IpAddress,
                OldValues = oldValues,
                NewValues = newValues,
                ChangedColumns = changedCols,
                TimestampUtc = DateTime.UtcNow
            });
        }

        return items;
    }

    private static List<AuditTrailEntry> BuildAuditEntities(PendingAuditBatch batch)
    {
        var list = new List<AuditTrailEntry>(batch.Items.Count);
        foreach (var item in batch.Items)
        {
            var pkProp = item.Entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            var pkVal = pkProp?.CurrentValue?.ToString()
                        ?? pkProp?.OriginalValue?.ToString()
                        ?? "0";

            if (item.Action == "CREATE" && pkProp != null)
            {
                item.NewValues[pkProp.Metadata.Name] = pkProp.CurrentValue;
            }

            var resolvedSocieteId = item.EntitySocieteId ?? TryExtractSocieteId(item.Entry);
            var societeName = item.Entry.Entity is Societe soc ? soc.Nom : null;
            var changedProps = item.ChangedColumns.Count > 0 ? string.Join(",", item.ChangedColumns) : null;

            list.Add(new AuditTrailEntry
            {
                UserId = item.UserId,
                UserName = Truncate(item.UserName, 150),
                UserRole = Truncate(item.UserRole, 80),
                SocieteId = resolvedSocieteId,
                SocieteName = Truncate(societeName, 150),
                EntityName = Truncate(item.TableName, 100) ?? "Unknown",
                EntityId = Truncate(pkVal, 100) ?? "0",
                Action = Truncate(item.Action, 40) ?? "UPDATE",
                BusinessActionLabel = Truncate(item.BusinessActionLabel, 200) ?? "Operation",
                TimestampUtc = item.TimestampUtc,
                OldValuesJson = AuditSanitizer.SerializeSanitized(item.OldValues),
                NewValuesJson = AuditSanitizer.SerializeSanitized(item.NewValues),
                ChangedProperties = Truncate(changedProps, 1000),
                TransactionId = Truncate(batch.TransactionId, 64),
                CorrelationId = Truncate(item.CorrelationId, 128),
                Source = "API_EF_INTERCEPTOR",
                IpAddress = Truncate(item.IpAddress, 64)
            });
        }
        return list;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value == null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private ActorSnapshot ExtractActorMetadata()
    {
        var ctx = _httpContextAccessor?.HttpContext;
        if (ctx == null)
        {
            return new ActorSnapshot
            {
                UserName = "Système Backend",
                UserRole = "System",
                CorrelationId = Guid.NewGuid().ToString("N")[..12]
            };
        }

        var user = ctx.User;
        int? userId = null;
        var uidStr = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? user?.FindFirst("sub")?.Value
                  ?? user?.FindFirst("id")?.Value;
        if (int.TryParse(uidStr, out int uid) && uid > 0) userId = uid;

        int? societeId = null;
        var sidStr = user?.FindFirst("SocieteId")?.Value;
        if (int.TryParse(sidStr, out int sid) && sid > 0) societeId = sid;

        // Si SuperAdmin avec X-Tenant-Id valide, conserver le contexte cible
        var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? (user?.Identity?.IsAuthenticated == true ? "Utilisateur" : "System");
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            var header = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (int.TryParse(header, out int hSid) && hSid > 0) societeId = hSid;
        }

        var userName = user?.FindFirst("name")?.Value
                    ?? user?.FindFirst(ClaimTypes.Email)?.Value
                    ?? (user?.Identity?.IsAuthenticated == true ? "Utilisateur Authentifié" : "Système / Anonyme");

        var corrId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? ctx.TraceIdentifier;
        var ip = ctx.Connection?.RemoteIpAddress?.ToString();

        return new ActorSnapshot
        {
            UserId = userId,
            UserName = userName,
            UserRole = role,
            SocieteId = societeId,
            CorrelationId = corrId,
            IpAddress = ip
        };
    }

    private static int? TryExtractSocieteId(EntityEntry entry)
    {
        var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "SocieteId");
        var val = prop?.CurrentValue ?? prop?.OriginalValue;
        if (val != null && int.TryParse(val.ToString(), out int sid) && sid > 0)
            return sid;

        if (entry.Entity is Societe soc && soc.Id > 0)
            return soc.Id;

        return null;
    }

    private static string BuildBusinessLabel(string tableName, string action, List<string> changedCols)
    {
        return (tableName, action) switch
        {
            ("Equipements", "CREATE") => "Création d'un équipement",
            ("Equipements", "UPDATE") => changedCols.Contains("Etat") ? "Changement d'état d'un équipement" : "Modification d'un équipement",
            ("Equipements", "DELETE") => "Suppression d'un équipement",
            ("OrdresTravail", "CREATE") => "Création d'un ordre de travail",
            ("OrdresTravail", "STATUS_CHANGE") => "Changement de statut d'un OT",
            ("OrdresTravail", "UPDATE") => "Modification d'un ordre de travail",
            ("OrdresTravail", "DELETE") => "Suppression d'un ordre de travail",
            ("DemandesIntervention", "CREATE") => "Déclaration d'incident / panne",
            ("DemandesIntervention", "UPDATE") => changedCols.Contains("Statut") ? "Changement de statut d'incident" : "Modification d'incident",
            ("Documents", "CREATE") => "Ajout d'un document technique",
            ("Documents", "DELETE") => "Suppression d'un document",
            ("Users", "CREATE") => "Création d'un compte utilisateur",
            ("Users", "UPDATE") => "Modification d'un compte utilisateur",
            ("RolePermissions", _) => "Modification des permissions RBAC",
            ("Campagnes", "CREATE") => "Création d'une campagne industrielle",
            ("Campagnes", "UPDATE") => changedCols.Contains("Etat") ? "Clôture / changement d'état de campagne" : "Modification d'une campagne",
            ("Pieces", "UPDATE") => "Mise à jour d'une pièce de stock",
            ("MouvementsStock", "CREATE") => "Mouvement de stock enregistré",
            ("Societes", _) => "Opération administrative sur société",
            _ => $"{action} sur {tableName}"
        };
    }

    private static string? TryReadPostgresXid(DbContext context)
    {
        try
        {
            var conn = context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) return null;
            using var cmd = conn.CreateCommand();
            cmd.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = "SELECT pg_current_xact_id()::xid::text";
            return cmd.ExecuteScalar()?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> TryReadPostgresXidAsync(DbContext context, CancellationToken ct)
    {
        try
        {
            var conn = context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) return null;
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = "SELECT pg_current_xact_id()::xid::text";
            var res = await cmd.ExecuteScalarAsync(ct);
            return res?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private sealed class PendingAuditBatch
    {
        public IDbContextTransaction? OwnedTransaction { get; set; }
        public string? TransactionId { get; set; }
        public List<PendingAuditItem> Items { get; set; } = new();
    }

    private sealed class PendingAuditItem
    {
        public EntityEntry Entry { get; set; } = null!;
        public string TableName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string BusinessActionLabel { get; set; } = string.Empty;
        public int? EntitySocieteId { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserRole { get; set; }
        public string? CorrelationId { get; set; }
        public string? IpAddress { get; set; }
        public Dictionary<string, object?> OldValues { get; set; } = new();
        public Dictionary<string, object?> NewValues { get; set; } = new();
        public List<string> ChangedColumns { get; set; } = new();
        public DateTime TimestampUtc { get; set; }
    }

    private sealed class ActorSnapshot
    {
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserRole { get; set; }
        public int? SocieteId { get; set; }
        public string? CorrelationId { get; set; }
        public string? IpAddress { get; set; }
    }
}
