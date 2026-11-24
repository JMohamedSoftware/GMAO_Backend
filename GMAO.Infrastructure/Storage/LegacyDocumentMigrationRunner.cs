using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GMAO.Infrastructure.Storage;

public sealed class LegacyMigrationOptions
{
    public bool Execute { get; set; }
    public string WebRootPath { get; set; } = string.Empty;
    public int? FilterSocieteId { get; set; }
    public int? Limit { get; set; }
    public string? ManifestOutputPath { get; set; }
}

public sealed class DocumentInventoryItem
{
    public int DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int? ResolvedSocieteId { get; set; }
    public string CurrentStorageProvider { get; set; } = "Local";
    public string CurrentChemin { get; set; } = string.Empty;
    public long DbSizeBytes { get; set; }
    public bool PhysicalFileExists { get; set; }
    public long? PhysicalSizeBytes { get; set; }
    public string? PhysicalSha256 { get; set; }
    public string Status { get; set; } = string.Empty; // ReadyToMigrate | AlreadySupabase | MissingPhysicalFile | UnresolvedTenant | Migrated | Skipped | Error
    public string? TargetStorageKey { get; set; }
    public string? Message { get; set; }
}

public sealed class LegacyPhotoReferenceItem
{
    public string EntityType { get; set; } = string.Empty; // Equipement | DemandeIntervention | User
    public int EntityId { get; set; }
    public int? SocieteId { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public bool PhysicalFileExists { get; set; }
}

public sealed class MigrationManifestEntry
{
    public int DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int? OldSocieteId { get; set; }
    public string OldChemin { get; set; } = string.Empty;
    public string OldStorageProvider { get; set; } = "Local";
    public string? OldBucketName { get; set; }
    public string? OldSha256Checksum { get; set; }
    public int NewSocieteId { get; set; }
    public string NewChemin { get; set; } = string.Empty;
    public string NewStorageProvider { get; set; } = "Supabase";
    public string? NewBucketName { get; set; }
    public string NewSha256Checksum { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime MigratedAtUtc { get; set; }
}

public sealed class LegacyMigrationReport
{
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CompletedAtUtc { get; set; }
    public string Mode { get; set; } = "DryRun"; // DryRun | Execute | Rollback
    public string WebRootPath { get; set; } = string.Empty;
    public int TotalDocumentsInDb { get; set; }
    public int AlreadySupabaseCount { get; set; }
    public int ReadyToMigrateCount { get; set; }
    public int MigratedCount { get; set; }
    public int MissingPhysicalFileCount { get; set; }
    public int UnresolvedTenantCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> OrphanPhysicalFiles { get; set; } = new();
    public List<LegacyPhotoReferenceItem> LegacyLocalPhotoReferences { get; set; } = new();
    public List<DocumentInventoryItem> Items { get; set; } = new();
    public List<MigrationManifestEntry> ManifestEntries { get; set; } = new();
    public string? ManifestFilePath { get; set; }
}

public sealed class SystemMigrationTenantContext : ITenantContextService
{
    public int? CurrentSocieteId => null;
    public bool IsSuperAdmin => true;
    public bool IsImpersonating => false;
    public int GetRequiredTenantId() =>
        throw new InvalidOperationException("Le contexte système transversal n'est pas rattaché à une société unique.");
}

/// <summary>
/// Outil d'inventaire et de migration sécurisée des documents historiques (disque local -> Supabase Storage privé).
/// Garantit :
/// - Mode Dry-Run par défaut (aucune écriture sans --execute).
/// - Vérification d'intégrité SHA-256 round-trip (upload + lecture + comparaison hash/taille) avant mise à jour PostgreSQL.
/// - Génération d'un manifeste JSON de rollback complet.
/// - Conservation intégrale des fichiers locaux historiques (aucune suppression automatique).
/// </summary>
public sealed class LegacyDocumentMigrationRunner
{
    private readonly GmaoDbContext _db;
    private readonly IFileStorageService? _targetStorage;
    private readonly ILogger<LegacyDocumentMigrationRunner> _logger;

    public LegacyDocumentMigrationRunner(
        GmaoDbContext db,
        IFileStorageService? targetStorage,
        ILogger<LegacyDocumentMigrationRunner> logger)
    {
        _db = db;
        _targetStorage = targetStorage;
        _logger = logger;
    }

    public async Task<LegacyMigrationReport> RunAsync(LegacyMigrationOptions options, CancellationToken ct = default)
    {
        var report = new LegacyMigrationReport
        {
            StartedAtUtc = DateTime.UtcNow,
            Mode = options.Execute ? "Execute" : "DryRun",
            WebRootPath = string.IsNullOrWhiteSpace(options.WebRootPath)
                ? Path.GetFullPath(Directory.GetCurrentDirectory())
                : Path.GetFullPath(options.WebRootPath)
        };

        if (options.Execute && _targetStorage == null)
        {
            throw new InvalidOperationException("Aucun fournisseur de stockage cible (Supabase) n'est configuré pour le mode --execute.");
        }

        var docs = await _db.Documents
            .IgnoreQueryFilters()
            .Include(d => d.Equipement)
            .Include(d => d.OT)
            .Include(d => d.PlanPreventif)
                .ThenInclude(p => p!.Equipement)
            .Include(d => d.Incident)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);

        var userSocietes = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.SocieteId != null && u.SocieteId > 0)
            .Select(u => new { u.Id, u.SocieteId })
            .ToDictionaryAsync(u => u.Id, u => u.SocieteId, ct);

        report.TotalDocumentsInDb = docs.Count;

        var knownLocalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in docs)
        {
            if (string.Equals(d.StorageProvider, "Local", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(d.Chemin))
            {
                var physical = ResolveLocalPhysicalPath(report.WebRootPath, d.Chemin);
                if (physical != null) knownLocalPaths.Add(Path.GetFullPath(physical));
            }
        }

        // Inventorier aussi les chemins locaux potentiels dans PhotoUrl / AvatarUrl
        await CollectLegacyPhotoReferencesAsync(report, knownLocalPaths, ct);

        // Inventorier les fichiers orphelins sur le disque local
        CollectOrphanPhysicalFiles(report, knownLocalPaths);

        int processed = 0;
        foreach (var doc in docs)
        {
            int? resolvedSocieteId = ResolveTenantId(doc, userSocietes);
            if (options.FilterSocieteId.HasValue && resolvedSocieteId != options.FilterSocieteId.Value)
                continue;

            var item = new DocumentInventoryItem
            {
                DocumentId = doc.Id,
                FileName = doc.Nom,
                ResolvedSocieteId = resolvedSocieteId,
                CurrentStorageProvider = doc.StorageProvider,
                CurrentChemin = doc.Chemin,
                DbSizeBytes = doc.TailleFichierOctets
            };

            if (string.Equals(doc.StorageProvider, "Supabase", StringComparison.OrdinalIgnoreCase))
            {
                item.Status = "AlreadySupabase";
                item.Message = "Déjà migré vers Supabase Storage.";
                report.AlreadySupabaseCount++;
                report.Items.Add(item);
                continue;
            }

            var physicalPath = ResolveLocalPhysicalPath(report.WebRootPath, doc.Chemin);
            if (physicalPath == null || !File.Exists(physicalPath))
            {
                item.PhysicalFileExists = false;
                item.Status = "MissingPhysicalFile";
                item.Message = physicalPath == null
                    ? "Chemin invalide ou hors du périmètre autorisé."
                    : $"Fichier physique introuvable sur disque : {physicalPath}";
                report.MissingPhysicalFileCount++;
                report.Items.Add(item);
                continue;
            }

            item.PhysicalFileExists = true;
            var fi = new FileInfo(physicalPath);
            item.PhysicalSizeBytes = fi.Length;

            if (!resolvedSocieteId.HasValue || resolvedSocieteId.Value <= 0)
            {
                item.Status = "UnresolvedTenant";
                item.Message = "Impossible de déterminer le SocieteId propriétaire sans risque de mélange multi-tenant.";
                report.UnresolvedTenantCount++;
                report.Items.Add(item);
                continue;
            }

            string ext = Path.GetExtension(doc.Nom);
            if (string.IsNullOrWhiteSpace(ext))
                ext = Path.GetExtension(physicalPath);
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".bin";

            item.PhysicalSha256 = await ComputeFileSha256Async(physicalPath, ct);
            item.TargetStorageKey = $"tenants/{resolvedSocieteId.Value}/documents/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            item.Status = "ReadyToMigrate";
            report.ReadyToMigrateCount++;

            if (options.Execute && (!options.Limit.HasValue || processed < options.Limit.Value))
            {
                await MigrateSingleDocumentAsync(doc, item, physicalPath, resolvedSocieteId.Value, report, ct);
                processed++;
            }

            report.Items.Add(item);
        }

        if (options.Execute && report.ManifestEntries.Count > 0)
        {
            var manifestPath = string.IsNullOrWhiteSpace(options.ManifestOutputPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), $"migration-manifest-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json")
                : Path.GetFullPath(options.ManifestOutputPath);

            var manifestDir = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrEmpty(manifestDir) && !Directory.Exists(manifestDir))
                Directory.CreateDirectory(manifestDir);

            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(report.ManifestEntries, new JsonSerializerOptions { WriteIndented = true }),
                ct);
            report.ManifestFilePath = manifestPath;
        }

        report.CompletedAtUtc = DateTime.UtcNow;
        return report;
    }

    public async Task<(int restored, int skipped)> RollbackFromManifestAsync(
        string manifestFilePath,
        string? webRootPath,
        CancellationToken ct = default)
    {
        if (!File.Exists(manifestFilePath))
            throw new FileNotFoundException("Manifeste de rollback introuvable.", manifestFilePath);

        var json = await File.ReadAllTextAsync(manifestFilePath, ct);
        var entries = JsonSerializer.Deserialize<List<MigrationManifestEntry>>(json) ?? new();

        int restored = 0;
        int skipped = 0;

        foreach (var entry in entries)
        {
            var doc = await _db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == entry.DocumentId, ct);
            if (doc == null)
            {
                skipped++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(webRootPath))
            {
                var physical = ResolveLocalPhysicalPath(webRootPath, entry.OldChemin);
                if (physical == null || !File.Exists(physical))
                {
                    _logger.LogWarning("Rollback ignoré pour Document Id={DocumentId} : fichier local original introuvable ({Path}).", entry.DocumentId, entry.OldChemin);
                    skipped++;
                    continue;
                }
            }

            doc.Chemin = entry.OldChemin;
            doc.StorageProvider = entry.OldStorageProvider;
            doc.BucketName = entry.OldBucketName;
            doc.SocieteId = entry.OldSocieteId;
            doc.Sha256Checksum = entry.OldSha256Checksum;
            await _db.SaveChangesAsync(ct);
            restored++;
        }

        return (restored, skipped);
    }

    private async Task MigrateSingleDocumentAsync(
        Document doc,
        DocumentInventoryItem item,
        string physicalPath,
        int resolvedSocieteId,
        LegacyMigrationReport report,
        CancellationToken ct)
    {
        if (_targetStorage == null || string.IsNullOrWhiteSpace(item.TargetStorageKey) || string.IsNullOrWhiteSpace(item.PhysicalSha256))
            return;

        try
        {
            var mime = string.IsNullOrWhiteSpace(doc.MimeType) ? "application/octet-stream" : doc.MimeType!;
            StoredFileResult uploaded;

            await using (var fs = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                uploaded = await _targetStorage.UploadAtPathAsync(fs, item.TargetStorageKey, mime, ct);
            }

            // Vérification d'intégrité round-trip : relecture + SHA-256 + taille avant écriture DB
            var downloaded = await _targetStorage.DownloadAsync(uploaded.StoragePath, ct);
            if (downloaded == null)
            {
                await TryCompensationDeleteAsync(uploaded.StoragePath, ct);
                item.Status = "Error";
                item.Message = "Vérification round-trip échouée : objet introuvable après upload.";
                report.ErrorCount++;
                return;
            }

            string verifiedSha256;
            long verifiedSize;
            await using (downloaded.Value.stream)
            {
                using var ms = new MemoryStream();
                await downloaded.Value.stream.CopyToAsync(ms, ct);
                verifiedSize = ms.Length;
                verifiedSha256 = Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
            }

            if (!string.Equals(verifiedSha256, item.PhysicalSha256, StringComparison.OrdinalIgnoreCase) ||
                verifiedSize != item.PhysicalSizeBytes)
            {
                await TryCompensationDeleteAsync(uploaded.StoragePath, ct);
                item.Status = "Error";
                item.Message = $"Incohérence SHA-256 ou taille lors de la vérification round-trip (local={item.PhysicalSha256}, distant={verifiedSha256}).";
                report.ErrorCount++;
                return;
            }

            var manifestEntry = new MigrationManifestEntry
            {
                DocumentId = doc.Id,
                FileName = doc.Nom,
                OldSocieteId = doc.SocieteId,
                OldChemin = doc.Chemin,
                OldStorageProvider = doc.StorageProvider,
                OldBucketName = doc.BucketName,
                OldSha256Checksum = doc.Sha256Checksum,
                NewSocieteId = resolvedSocieteId,
                NewChemin = uploaded.StoragePath,
                NewStorageProvider = uploaded.Provider,
                NewBucketName = uploaded.BucketName,
                NewSha256Checksum = verifiedSha256,
                FileSizeBytes = verifiedSize,
                MigratedAtUtc = DateTime.UtcNow
            };

            // Mise à jour PostgreSQL UNIQUEMENT après vérification d'intégrité réussie
            doc.SocieteId = resolvedSocieteId;
            doc.Chemin = uploaded.StoragePath;
            doc.StorageProvider = uploaded.Provider;
            doc.BucketName = uploaded.BucketName;
            doc.Sha256Checksum = verifiedSha256;
            doc.TailleFichierOctets = verifiedSize;

            await _db.SaveChangesAsync(ct);

            report.ManifestEntries.Add(manifestEntry);
            item.Status = "Migrated";
            item.Message = $"Migré et vérifié SHA-256 ({verifiedSha256[..12]}...). Ancien fichier local conservé.";
            report.MigratedCount++;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec migration Document Id={DocumentId}", doc.Id);
            item.Status = "Error";
            item.Message = ex.Message;
            report.ErrorCount++;
        }
    }

    private async Task TryCompensationDeleteAsync(string storageKey, CancellationToken ct)
    {
        if (_targetStorage == null) return;
        try
        {
            await _targetStorage.DeleteAsync(storageKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nettoyage compensatoire impossible pour {Key}", storageKey);
        }
    }

    private static int? ResolveTenantId(Document doc, IReadOnlyDictionary<int, int?> userSocietes)
    {
        if (doc.SocieteId is > 0) return doc.SocieteId;
        if (doc.Equipement?.SocieteId is > 0) return doc.Equipement.SocieteId;
        if (doc.OT?.SocieteId is > 0) return doc.OT.SocieteId;
        if (doc.PlanPreventif?.Equipement?.SocieteId is > 0) return doc.PlanPreventif.Equipement.SocieteId;
        if (doc.Incident?.SocieteId is > 0) return doc.Incident.SocieteId;
        if (userSocietes.TryGetValue(doc.UploadedById, out var uSid) && uSid is > 0) return uSid;
        return null;
    }

    private async Task CollectLegacyPhotoReferencesAsync(
        LegacyMigrationReport report,
        HashSet<string> knownLocalPaths,
        CancellationToken ct)
    {
        var equipPhotos = await _db.Equipements.IgnoreQueryFilters()
            .Where(e => !string.IsNullOrEmpty(e.PhotoUrl) && !e.PhotoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Select(e => new { e.Id, e.SocieteId, e.PhotoUrl })
            .ToListAsync(ct);

        foreach (var e in equipPhotos)
        {
            var physical = ResolveLocalPhysicalPath(report.WebRootPath, e.PhotoUrl!);
            if (physical != null) knownLocalPaths.Add(Path.GetFullPath(physical));
            report.LegacyLocalPhotoReferences.Add(new LegacyPhotoReferenceItem
            {
                EntityType = "Equipement",
                EntityId = e.Id,
                SocieteId = e.SocieteId,
                RelativePath = e.PhotoUrl!,
                PhysicalFileExists = physical != null && File.Exists(physical)
            });
        }

        var incPhotos = await _db.DemandesIntervention.IgnoreQueryFilters()
            .Where(i => !string.IsNullOrEmpty(i.PhotoUrl) && !i.PhotoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Select(i => new { i.Id, i.SocieteId, i.PhotoUrl })
            .ToListAsync(ct);

        foreach (var i in incPhotos)
        {
            var physical = ResolveLocalPhysicalPath(report.WebRootPath, i.PhotoUrl!);
            if (physical != null) knownLocalPaths.Add(Path.GetFullPath(physical));
            report.LegacyLocalPhotoReferences.Add(new LegacyPhotoReferenceItem
            {
                EntityType = "DemandeIntervention",
                EntityId = i.Id,
                SocieteId = i.SocieteId,
                RelativePath = i.PhotoUrl!,
                PhysicalFileExists = physical != null && File.Exists(physical)
            });
        }
    }

    private static void CollectOrphanPhysicalFiles(LegacyMigrationReport report, HashSet<string> knownLocalPaths)
    {
        var uploadsDir = Path.Combine(report.WebRootPath, "uploads");
        if (!Directory.Exists(uploadsDir)) return;

        foreach (var file in Directory.EnumerateFiles(uploadsDir, "*", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(file);
            if (!knownLocalPaths.Contains(full))
            {
                report.OrphanPhysicalFiles.Add(Path.GetRelativePath(report.WebRootPath, full).Replace('\\', '/'));
            }
        }
    }

    public static string? ResolveLocalPhysicalPath(string webRootPath, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(webRootPath))
            return null;

        var clean = relativePath.Replace('\\', '/').TrimStart('/');
        if (clean.Contains("..", StringComparison.Ordinal))
            return null;

        var rootFull = Path.GetFullPath(webRootPath);
        var uploadsRoot = Path.GetFullPath(Path.Combine(rootFull, "uploads"));

        string candidate = clean.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(Path.Combine(rootFull, clean))
            : Path.GetFullPath(Path.Combine(uploadsRoot, clean));

        if (!candidate.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate, uploadsRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return candidate;
    }

    private static async Task<string> ComputeFileSha256Async(string filePath, CancellationToken ct)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
