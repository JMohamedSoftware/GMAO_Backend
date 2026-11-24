using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace GMAO.Infrastructure.Storage;

/// <summary>
/// Validation stricte des fichiers entrants :
/// - Whitelist d'extensions et types MIME autorisés.
/// - Vérification des octets magiques (Magic Bytes) contre l'usurpation d'extension (.exe déguisé en .pdf).
/// - Calcul d'empreinte SHA-256 avant transfert.
/// - Génération systématique d'une clé de stockage aléatoire isolée par tenant.
/// </summary>
public static class FileValidationHelper
{
    public const long DefaultMaxBytes = 20L * 1024 * 1024; // 20 Mo

    private static readonly Dictionary<string, string[]> AllowedExtensionsAndMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"]  = new[] { "application/pdf" },
        [".png"]  = new[] { "image/png" },
        [".jpg"]  = new[] { "image/jpeg" },
        [".jpeg"] = new[] { "image/jpeg" },
        [".webp"] = new[] { "image/webp" },
        [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/zip", "application/octet-stream" },
        [".xlsx"] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/zip", "application/octet-stream" },
        [".doc"]  = new[] { "application/msword", "application/octet-stream" },
        [".xls"]  = new[] { "application/vnd.ms-excel", "application/octet-stream" },
        [".txt"]  = new[] { "text/plain" },
        [".csv"]  = new[] { "text/csv", "text/plain", "application/vnd.ms-excel" },
        [".dwg"]  = new[] { "application/acad", "image/vnd.dwg", "application/octet-stream" }
    };

    public static string BuildTenantStoragePath(int societeId, string originalFileName)
    {
        if (societeId <= 0)
            throw new ArgumentException("Identifiant société invalide pour le stockage documentaire.", nameof(societeId));

        var ext = Path.GetExtension(originalFileName)?.ToLowerInvariant() ?? string.Empty;
        if (!AllowedExtensionsAndMimes.ContainsKey(ext))
            throw new InvalidOperationException($"Extension de fichier non autorisée : '{ext}'.");

        return $"tenants/{societeId}/documents/{Guid.NewGuid():N}{ext}";
    }

    public static void ValidateExtensionAndSize(string fileName, string? contentType, long fileSize, long maxBytes = DefaultMaxBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("Nom de fichier manquant.");

        if (fileSize <= 0)
            throw new InvalidOperationException("Le fichier fourni est vide.");

        if (fileSize > maxBytes)
            throw new InvalidOperationException($"Le fichier dépasse la taille maximale autorisée ({maxBytes / (1024 * 1024)} Mo).");

        var ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;
        if (!AllowedExtensionsAndMimes.TryGetValue(ext, out var allowedMimes))
            throw new InvalidOperationException($"Extension '{ext}' interdite. Formats acceptés : {string.Join(", ", AllowedExtensionsAndMimes.Keys)}.");

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var normalizedMime = contentType.Split(';')[0].Trim().ToLowerInvariant();
            if (!allowedMimes.Contains(normalizedMime) && normalizedMime != "application/octet-stream")
            {
                throw new InvalidOperationException($"Le type MIME '{normalizedMime}' ne correspond pas à l'extension '{ext}'.");
            }
        }
    }

    /// <summary>
    /// Lit le flux en mémoire tampon (ou positionne au début), valide les Magic Bytes réels
    /// et retourne l'empreinte SHA-256 ainsi qu'un flux prêt pour l'upload.
    /// </summary>
    public static async Task<(MemoryStream BufferedStream, string Sha256Hex, string VerifiedMimeType)> ValidateSignatureAndHashAsync(
        Stream sourceStream,
        string fileName,
        string? declaredContentType,
        long maxBytes = DefaultMaxBytes,
        CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;
        ValidateExtensionAndSize(fileName, declaredContentType, sourceStream.CanSeek ? sourceStream.Length : maxBytes, maxBytes);

        var ms = new MemoryStream();
        await sourceStream.CopyToAsync(ms, ct);
        if (ms.Length == 0)
        {
            ms.Dispose();
            throw new InvalidOperationException("Le fichier envoyé est vide.");
        }
        if (ms.Length > maxBytes)
        {
            ms.Dispose();
            throw new InvalidOperationException($"Le fichier dépasse la taille maximale autorisée ({maxBytes / (1024 * 1024)} Mo).");
        }

        var bytes = ms.GetBuffer();
        int len = (int)ms.Length;

        if (!VerifyMagicBytes(ext, bytes, len))
        {
            ms.Dispose();
            throw new InvalidOperationException($"La signature binaire du fichier ne correspond pas au format déclaré '{ext}'. Fichier rejeté pour raison de sécurité.");
        }

        ms.Position = 0;
        var hashBytes = SHA256.HashData(new ReadOnlySpan<byte>(bytes, 0, len));
        var sha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var canonicalMime = ResolveCanonicalMimeType(ext, declaredContentType);

        return (ms, sha256, canonicalMime);
    }

    public static bool VerifyMagicBytes(string ext, byte[] buf, int length)
    {
        // Rejeter systématiquement les exécutables PE/ELF déguisés
        if (length >= 2 && buf[0] == 0x4D && buf[1] == 0x5A) return false; // MZ (EXE/DLL Windows)
        if (length >= 4 && buf[0] == 0x7F && buf[1] == 0x45 && buf[2] == 0x4C && buf[3] == 0x46) return false; // ELF

        return ext.ToLowerInvariant() switch
        {
            ".pdf" => length >= 5 && buf[0] == 0x25 && buf[1] == 0x50 && buf[2] == 0x44 && buf[3] == 0x46 && buf[4] == 0x2D, // %PDF-
            ".png" => length >= 8 && buf[0] == 0x89 && buf[1] == 0x50 && buf[2] == 0x4E && buf[3] == 0x47 && buf[4] == 0x0D && buf[5] == 0x0A && buf[6] == 0x1A && buf[7] == 0x0A,
            ".jpg" or ".jpeg" => length >= 3 && buf[0] == 0xFF && buf[1] == 0xD8 && buf[2] == 0xFF,
            ".webp" => length >= 12 && buf[0] == 0x52 && buf[1] == 0x49 && buf[2] == 0x46 && buf[3] == 0x46 && buf[8] == 0x57 && buf[9] == 0x45 && buf[10] == 0x42 && buf[11] == 0x50, // RIFF....WEBP
            ".docx" or ".xlsx" => length >= 4 && buf[0] == 0x50 && buf[1] == 0x4B && buf[2] == 0x03 && buf[3] == 0x04, // PK..
            ".doc" or ".xls" => length >= 8 && buf[0] == 0xD0 && buf[1] == 0xCF && buf[2] == 0x11 && buf[3] == 0xE0, // OLE2 Compound
            ".dwg" => length >= 4 && buf[0] == 0x41 && buf[1] == 0x43 && buf[2] == 0x31 && buf[3] == 0x30, // AC10
            ".txt" or ".csv" => !HasNullBytes(buf, Math.Min(length, 4096)),
            _ => false
        };
    }

    private static bool HasNullBytes(byte[] buf, int sampleLen)
    {
        for (int i = 0; i < sampleLen; i++)
        {
            if (buf[i] == 0x00) return true;
        }
        return false;
    }

    private static string ResolveCanonicalMimeType(string ext, string? declared)
    {
        return ext.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".doc" => "application/msword",
            ".xls" => "application/vnd.ms-excel",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".dwg" => "application/acad",
            _ => string.IsNullOrWhiteSpace(declared) ? "application/octet-stream" : declared
        };
    }
}
