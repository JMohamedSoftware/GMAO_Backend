using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace GMAO.Infrastructure.Storage;

/// <summary>
/// Routeur de stockage documentaire assurant :
/// 1. L'utilisation automatique de Supabase Storage (bucket privé) pour les nouveaux dépôts dès que les secrets sont configurés.
/// 2. La rétrocompatibilité transparente pour le téléchargement/suppression des anciens fichiers locaux ("Local").
/// </summary>
public class HybridFileStorageService : IFileStorageService
{
    private readonly SupabaseFileStorageService _supabase;
    private readonly LocalFileStorageService _local;
    private readonly SupabaseStorageOptions _options;

    public HybridFileStorageService(
        SupabaseFileStorageService supabase,
        LocalFileStorageService local,
        IOptions<SupabaseStorageOptions> options)
    {
        _supabase = supabase;
        _local = local;
        _options = options.Value;
    }

    public string ProviderName => ResolveActiveWriteService().ProviderName;

    public IFileStorageService ResolveActiveWriteService()
    {
        if (string.Equals(_options.ProviderMode, "Local", StringComparison.OrdinalIgnoreCase))
            return _local;

        if (string.Equals(_options.ProviderMode, "Supabase", StringComparison.OrdinalIgnoreCase))
            return _supabase;

        return _options.IsConfigured ? _supabase : _local;
    }

    public IFileStorageService ResolveByProvider(string? providerName)
    {
        if (string.Equals(providerName, "Supabase", StringComparison.OrdinalIgnoreCase))
            return _supabase;

        if (string.Equals(providerName, "Local", StringComparison.OrdinalIgnoreCase))
            return _local;

        return ResolveActiveWriteService();
    }

    public Task<StoredFileResult> UploadAsync(Stream fileStream, string fileName, string contentType, int societeId, CancellationToken ct = default)
        => ResolveActiveWriteService().UploadAsync(fileStream, fileName, contentType, societeId, ct);

    public Task<StoredFileResult> UploadAtPathAsync(Stream fileStream, string storagePath, string contentType, CancellationToken ct = default)
        => ResolveActiveWriteService().UploadAtPathAsync(fileStream, storagePath, contentType, ct);

    public Task<(Stream stream, string contentType, string fileName)?> DownloadAsync(string storagePath, CancellationToken ct = default)
        => ResolveActiveWriteService().DownloadAsync(storagePath, ct);

    public Task<bool> DeleteAsync(string storagePath, CancellationToken ct = default)
        => ResolveActiveWriteService().DeleteAsync(storagePath, ct);

    public Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default)
        => ResolveActiveWriteService().ExistsAsync(storagePath, ct);

    public Task<StorageFileMetadata?> GetMetadataAsync(string storagePath, CancellationToken ct = default)
        => ResolveActiveWriteService().GetMetadataAsync(storagePath, ct);

    public Task<SignedUrlResult?> CreateSignedUrlAsync(string storagePath, int expiresInSeconds = 120, CancellationToken ct = default)
        => ResolveActiveWriteService().CreateSignedUrlAsync(storagePath, expiresInSeconds, ct);
}
