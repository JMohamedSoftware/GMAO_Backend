using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace GMAO.Infrastructure.Services;

/// <summary>
/// Implémentation locale de secours et de rétrocompatibilité de IFileStorageService.
/// Partitionne les nouveaux fichiers sous wwwroot/uploads/tenants/{societeId}/documents/{guid}.{ext}
/// tout en sachant lire les chemins historiques uploads/documents/ ou uploads/tenants/.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _webRoot;
    private readonly string _baseUploadDir;
    private readonly ILogger<LocalFileStorageService> _logger;

    public string ProviderName => "Local";

    public LocalFileStorageService(ILogger<LocalFileStorageService> logger, string? rootPath = null)
    {
        _logger = logger;
        _webRoot = !string.IsNullOrWhiteSpace(rootPath)
            ? Path.GetFullPath(rootPath)
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
        _baseUploadDir = Path.GetFullPath(Path.Combine(_webRoot, "uploads"));

        if (!Directory.Exists(_baseUploadDir))
        {
            Directory.CreateDirectory(_baseUploadDir);
        }
    }

    public async Task<StoredFileResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        int societeId,
        CancellationToken ct = default)
    {
        var relativeKey = FileValidationHelper.BuildTenantStoragePath(societeId, fileName);
        return await UploadAtPathAsync(fileStream, relativeKey, contentType, ct);
    }

    public async Task<StoredFileResult> UploadAtPathAsync(
        Stream fileStream,
        string storagePath,
        string contentType,
        CancellationToken ct = default)
    {
        var fullPath = ResolveSafePhysicalPath(storagePath, forWrite: true)
            ?? throw new InvalidOperationException("Tentative de violation de chemin de stockage (Path Traversal détecté).");

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (fileStream.CanSeek) fileStream.Position = 0;

        long size = 0;
        string sha256Hex;
        await using (var dest = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(dest, ct);
            size = dest.Length;
        }

        await using (var readBack = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var hash = await SHA256.HashDataAsync(readBack, ct);
            sha256Hex = Convert.ToHexString(hash).ToLowerInvariant();
        }

        var normalized = storagePath.TrimStart('/', '\\').Replace("\\", "/");
        return new StoredFileResult
        {
            StoragePath = normalized,
            Provider = ProviderName,
            BucketName = null,
            FileSizeBytes = size,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? GetContentType(fullPath) : contentType,
            Sha256Checksum = sha256Hex
        };
    }

    public Task<(Stream stream, string contentType, string fileName)?> DownloadAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = ResolveSafePhysicalPath(storagePath, forWrite: false);
        if (fullPath == null || !File.Exists(fullPath))
        {
            if (fullPath != null)
                _logger.LogWarning("Fichier physique introuvable sur disque local : {Path}", fullPath);
            return Task.FromResult<(Stream, string, string)?>(null);
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var fileName = Path.GetFileName(fullPath);
        var contentType = GetContentType(fullPath);

        return Task.FromResult<(Stream, string, string)?>((stream, contentType, fileName));
    }

    public Task<bool> DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return Task.FromResult(true);

        try
        {
            var fullPath = ResolveSafePhysicalPath(storagePath, forWrite: false);
            if (fullPath == null) return Task.FromResult(false);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la suppression physique du fichier local : {Path}", storagePath);
            return Task.FromResult(false);
        }
    }

    public Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = ResolveSafePhysicalPath(storagePath, forWrite: false);
        return Task.FromResult(fullPath != null && File.Exists(fullPath));
    }

    public Task<StorageFileMetadata?> GetMetadataAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = ResolveSafePhysicalPath(storagePath, forWrite: false);
        if (fullPath == null || !File.Exists(fullPath))
            return Task.FromResult<StorageFileMetadata?>(null);

        var fi = new FileInfo(fullPath);
        return Task.FromResult<StorageFileMetadata?>(new StorageFileMetadata
        {
            Exists = true,
            StoragePath = storagePath,
            ContentType = GetContentType(fullPath),
            SizeBytes = fi.Length,
            LastModifiedUtc = fi.LastWriteTimeUtc
        });
    }

    public Task<SignedUrlResult?> CreateSignedUrlAsync(string storagePath, int expiresInSeconds = 120, CancellationToken ct = default)
    {
        // En mode Local, aucune URL externe signée n'existe : le frontend doit utiliser l'endpoint JWT authentifié /api/Document/{id}/file
        return Task.FromResult<SignedUrlResult?>(null);
    }

    private string? ResolveSafePhysicalPath(string storagePath, bool forWrite)
    {
        if (string.IsNullOrWhiteSpace(storagePath) || storagePath.Contains(".."))
            return null;

        var trimmed = storagePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string candidate = trimmed.StartsWith("uploads" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(Path.Combine(_webRoot, trimmed))
            : Path.GetFullPath(Path.Combine(_baseUploadDir, trimmed));

        if (!candidate.StartsWith(_baseUploadDir, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Tentative d'accès hors du répertoire de stockage ({Action}): {Path}", forWrite ? "Write" : "Read", storagePath);
            return null;
        }

        return candidate;
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".xls" => "application/vnd.ms-excel",
            ".dwg" => "application/acad",
            _ => "application/octet-stream"
        };
    }
}
