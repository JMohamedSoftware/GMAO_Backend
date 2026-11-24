using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GMAO.Infrastructure.Storage;

/// <summary>
/// Client REST privé pour Supabase Storage (/storage/v1) utilisant exclusivement la clé service_role côté serveur.
/// - Bucket privé : gmao-documents (aucun accès public direct).
/// - Arborescence : tenants/{SocieteId}/documents/{GeneratedFileId}.{ext}
/// - Supporte Upload, Download authentifié, Delete, Exists, Metadata et Signed URLs temporaires.
/// - Ne journalise ni n'expose JAMAIS la clé service_role.
/// </summary>
public class SupabaseFileStorageService : IFileStorageService
{
    private readonly HttpClient _http;
    private readonly SupabaseStorageOptions _options;
    private readonly ILogger<SupabaseFileStorageService> _logger;
    private volatile bool _bucketVerified;

    public string ProviderName => "Supabase";

    public SupabaseFileStorageService(
        HttpClient http,
        IOptions<SupabaseStorageOptions> options,
        ILogger<SupabaseFileStorageService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsurePrivateBucketAsync(CancellationToken ct = default)
    {
        if (_bucketVerified) return;
        EnsureConfigured();

        var bucketUrl = $"{BaseStorageUrl}/bucket/{Uri.EscapeDataString(_options.BucketName)}";
        using var getReq = CreateAuthorizedRequest(HttpMethod.Get, bucketUrl);
        using var getRes = await _http.SendAsync(getReq, ct);

        if (getRes.StatusCode == HttpStatusCode.NotFound)
        {
            var createUrl = $"{BaseStorageUrl}/bucket";
            using var createReq = CreateAuthorizedRequest(HttpMethod.Post, createUrl);
            var body = JsonSerializer.Serialize(new
            {
                id = _options.BucketName,
                name = _options.BucketName,
                @public = false,
                file_size_limit = FileValidationHelper.DefaultMaxBytes
            });
            createReq.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var createRes = await _http.SendAsync(createReq, ct);
            if (!createRes.IsSuccessStatusCode && createRes.StatusCode != HttpStatusCode.Conflict)
            {
                var errBody = await createRes.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"Impossible de créer le bucket privé Supabase '{_options.BucketName}' ({(int)createRes.StatusCode}): {errBody}", null, createRes.StatusCode);
            }
        }
        else if (!getRes.IsSuccessStatusCode)
        {
            var errBody = await getRes.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Erreur lors de la vérification du bucket Supabase ({(int)getRes.StatusCode}): {errBody}", null, getRes.StatusCode);
        }

        _bucketVerified = true;
    }

    public async Task<StoredFileResult> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        int societeId,
        CancellationToken ct = default)
    {
        var storagePath = FileValidationHelper.BuildTenantStoragePath(societeId, fileName);
        return await UploadAtPathAsync(fileStream, storagePath, contentType, ct);
    }

    public async Task<StoredFileResult> UploadAtPathAsync(
        Stream fileStream,
        string storagePath,
        string contentType,
        CancellationToken ct = default)
    {
        EnsureConfigured();
        ValidateObjectPath(storagePath);
        await EnsurePrivateBucketAsync(ct);

        if (fileStream.CanSeek) fileStream.Position = 0;

        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var sha256Hex = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var mime = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

        var url = $"{BaseStorageUrl}/object/{Uri.EscapeDataString(_options.BucketName)}/{EncodeObjectPath(storagePath)}";
        using var req = CreateAuthorizedRequest(HttpMethod.Post, url);
        req.Headers.Add("x-upsert", "false");

        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(mime);
        req.Content = content;

        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _logger.LogError("Échec upload Supabase Storage pour {Path}: HTTP {Status}", storagePath, (int)res.StatusCode);
            throw new HttpRequestException($"Supabase Storage indisponible ou erreur d'upload (HTTP {(int)res.StatusCode}): {err}", null, res.StatusCode);
        }

        return new StoredFileResult
        {
            StoragePath = storagePath,
            Provider = ProviderName,
            BucketName = _options.BucketName,
            FileSizeBytes = bytes.LongLength,
            ContentType = mime,
            Sha256Checksum = sha256Hex
        };
    }

    public async Task<(Stream stream, string contentType, string fileName)?> DownloadAsync(string storagePath, CancellationToken ct = default)
    {
        EnsureConfigured();
        ValidateObjectPath(storagePath);

        var url = $"{BaseStorageUrl}/object/{Uri.EscapeDataString(_options.BucketName)}/{EncodeObjectPath(storagePath)}";
        using var req = CreateAuthorizedRequest(HttpMethod.Get, url);

        // ResponseHeadersRead permet de streamer sans charger en mémoire, en transférant l'HttpResponseMessage au wrapper
        var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (res.StatusCode == HttpStatusCode.NotFound || res.StatusCode == HttpStatusCode.BadRequest)
        {
            res.Dispose();
            return null;
        }
        if (!res.IsSuccessStatusCode)
        {
            var status = res.StatusCode;
            res.Dispose();
            throw new HttpRequestException($"Échec du téléchargement depuis Supabase Storage (HTTP {(int)status}).", null, status);
        }

        var rawStream = await res.Content.ReadAsStreamAsync(ct);
        var wrapped = new HttpResponseStream(rawStream, res);
        var mime = res.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var fileName = Path.GetFileName(storagePath);
        return (wrapped, mime, fileName);
    }

    public async Task<bool> DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return true;
        EnsureConfigured();
        ValidateObjectPath(storagePath);

        var url = $"{BaseStorageUrl}/object/{Uri.EscapeDataString(_options.BucketName)}";
        using var req = CreateAuthorizedRequest(HttpMethod.Delete, url);
        req.Content = new StringContent(JsonSerializer.Serialize(new { prefixes = new[] { storagePath } }), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        if (res.IsSuccessStatusCode || res.StatusCode == HttpStatusCode.NotFound)
            return true;

        _logger.LogWarning("Échec suppression objet Supabase {Path}: HTTP {Status}", storagePath, (int)res.StatusCode);
        return false;
    }

    public async Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default)
    {
        var meta = await GetMetadataAsync(storagePath, ct);
        return meta?.Exists == true;
    }

    public async Task<StorageFileMetadata?> GetMetadataAsync(string storagePath, CancellationToken ct = default)
    {
        EnsureConfigured();
        ValidateObjectPath(storagePath);

        var url = $"{BaseStorageUrl}/object/info/{Uri.EscapeDataString(_options.BucketName)}/{EncodeObjectPath(storagePath)}";
        using var req = CreateAuthorizedRequest(HttpMethod.Get, url);
        using var res = await _http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.NotFound)
            return new StorageFileMetadata { Exists = false, StoragePath = storagePath };

        if (!res.IsSuccessStatusCode)
        {
            // Fallback HEAD sur /object/{bucket}/{path}
            var headUrl = $"{BaseStorageUrl}/object/{Uri.EscapeDataString(_options.BucketName)}/{EncodeObjectPath(storagePath)}";
            using var headReq = CreateAuthorizedRequest(HttpMethod.Head, headUrl);
            using var headRes = await _http.SendAsync(headReq, ct);
            if (headRes.StatusCode == HttpStatusCode.NotFound)
                return new StorageFileMetadata { Exists = false, StoragePath = storagePath };
            if (!headRes.IsSuccessStatusCode)
                return null;

            return new StorageFileMetadata
            {
                Exists = true,
                StoragePath = storagePath,
                ContentType = headRes.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                SizeBytes = headRes.Content.Headers.ContentLength ?? 0,
                ETag = headRes.Headers.ETag?.Tag,
                LastModifiedUtc = headRes.Content.Headers.LastModified
            };
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        long size = 0;
        if (root.TryGetProperty("metadata", out var metaObj) && metaObj.ValueKind == JsonValueKind.Object &&
            metaObj.TryGetProperty("size", out var szEl) && szEl.TryGetInt64(out var parsedSz))
        {
            size = parsedSz;
        }
        else if (root.TryGetProperty("size", out var topSz) && topSz.TryGetInt64(out var parsedTopSz))
        {
            size = parsedTopSz;
        }

        string mime = root.TryGetProperty("metadata", out var m2) && m2.ValueKind == JsonValueKind.Object &&
                      m2.TryGetProperty("mimetype", out var mt) ? (mt.GetString() ?? "application/octet-stream") : "application/octet-stream";

        return new StorageFileMetadata
        {
            Exists = true,
            StoragePath = storagePath,
            ContentType = mime,
            SizeBytes = size,
            ETag = root.TryGetProperty("eTag", out var et) ? et.GetString() : null
        };
    }

    public async Task<SignedUrlResult?> CreateSignedUrlAsync(string storagePath, int expiresInSeconds = 120, CancellationToken ct = default)
    {
        EnsureConfigured();
        ValidateObjectPath(storagePath);

        int ttl = Math.Clamp(expiresInSeconds > 0 ? expiresInSeconds : _options.SignedUrlExpiresSeconds, 30, 3600);
        var url = $"{BaseStorageUrl}/object/sign/{Uri.EscapeDataString(_options.BucketName)}/{EncodeObjectPath(storagePath)}";

        using var req = CreateAuthorizedRequest(HttpMethod.Post, url);
        req.Content = new StringContent(JsonSerializer.Serialize(new { expiresIn = ttl }), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Impossible de générer la Signed URL Supabase (HTTP {(int)res.StatusCode}): {err}", null, res.StatusCode);
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("signedURL", out var signedProp) &&
            !doc.RootElement.TryGetProperty("signedUrl", out signedProp))
        {
            return null;
        }

        var signedRelative = signedProp.GetString() ?? string.Empty;
        string fullSignedUrl;
        if (signedRelative.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            fullSignedUrl = signedRelative;
        }
        else if (signedRelative.StartsWith("/storage/v1", StringComparison.OrdinalIgnoreCase))
        {
            fullSignedUrl = $"{_options.Url.TrimEnd('/')}{signedRelative}";
        }
        else
        {
            fullSignedUrl = $"{BaseStorageUrl}/{signedRelative.TrimStart('/')}";
        }

        return new SignedUrlResult
        {
            Url = fullSignedUrl,
            ExpiresInSeconds = ttl,
            IsExternalSignedUrl = true,
            Provider = ProviderName
        };
    }

    private string BaseStorageUrl => $"{_options.Url.TrimEnd('/')}/storage/v1";

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
            throw new InvalidOperationException("Supabase Storage n'est pas configuré (Supabase:Url et Supabase:ServiceRoleKey requis dans les variables d'environnement serveur).");
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
        req.Headers.Add("apikey", _options.ServiceRoleKey);
        return req;
    }

    private static void ValidateObjectPath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath) || storagePath.Contains("..") || storagePath.Contains('\\'))
            throw new InvalidOperationException("Chemin objet invalide pour Supabase Storage.");
    }

    private static string EncodeObjectPath(string storagePath)
    {
        var segments = storagePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("/", Array.ConvertAll(segments, Uri.EscapeDataString));
    }

    private sealed class HttpResponseStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _response;

        public HttpResponseStream(Stream inner, HttpResponseMessage response)
        {
            _inner = inner;
            _response = response;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _inner.ReadAsync(buffer, offset, count, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => _inner.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            _response.Dispose();
            await base.DisposeAsync();
        }
    }
}
