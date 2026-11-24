using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GMAO.Application.Interfaces;

public class StoredFileResult
{
    public string StoragePath { get; set; } = string.Empty;
    public string Provider { get; set; } = "Local"; // "Supabase" ou "Local"
    public string? BucketName { get; set; }
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public string Sha256Checksum { get; set; } = string.Empty;
}

public class StorageFileMetadata
{
    public bool Exists { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string? ETag { get; set; }
    public DateTimeOffset? LastModifiedUtc { get; set; }
}

public class SignedUrlResult
{
    public string Url { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
    public bool IsExternalSignedUrl { get; set; }
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// Contrat d'abstraction du stockage documentaire (Local partitionné ou Supabase Storage privé).
/// Découple intégralement les contrôleurs du fournisseur de stockage sous-jacent.
/// </summary>
public interface IFileStorageService
{
    string ProviderName { get; }

    Task<StoredFileResult> UploadAsync(Stream fileStream, string fileName, string contentType, int societeId, CancellationToken ct = default);
    Task<StoredFileResult> UploadAtPathAsync(Stream fileStream, string storagePath, string contentType, CancellationToken ct = default);
    Task<(Stream stream, string contentType, string fileName)?> DownloadAsync(string storagePath, CancellationToken ct = default);
    Task<bool> DeleteAsync(string storagePath, CancellationToken ct = default);
    Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default);
    Task<StorageFileMetadata?> GetMetadataAsync(string storagePath, CancellationToken ct = default);
    Task<SignedUrlResult?> CreateSignedUrlAsync(string storagePath, int expiresInSeconds = 120, CancellationToken ct = default);
}
