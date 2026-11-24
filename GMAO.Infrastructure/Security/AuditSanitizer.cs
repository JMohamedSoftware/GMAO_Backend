using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GMAO.Infrastructure.Security;

/// <summary>
/// Utilitaire centralisé de filtrage et de masquage des données sensibles
/// avant persistance ou exposition dans l'Audit Trail applicatif et le module CDC WAL.
/// Garantit qu'aucun mot de passe, hash BCrypt, jeton JWT, Refresh Token ou secret API n'est exposé.
/// </summary>
public static class AuditSanitizer
{
    private static readonly HashSet<string> SensitiveColumnNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "PasswordHash",
        "MotDePasse",
        "Secret",
        "SecretKey",
        "ServiceRoleKey",
        "AnonKey",
        "ApiKey",
        "Token",
        "RefreshToken",
        "AccessToken",
        "JwtSecret",
        "Authorization",
        "ConnectionString",
        "ReplicationConnectionString"
    };

    private static readonly HashSet<string> ExcludedSystemTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "AuditTrailEntries",
        "CdcEvents",
        "CdcWorkerStates",
        "RefreshTokens",
        "JournauxSuperAdmin",
        "__EFMigrationsHistory"
    };

    public static bool IsSensitiveField(string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return false;
        if (SensitiveColumnNames.Contains(fieldName)) return true;

        var lower = fieldName.ToLowerInvariant();
        return lower.Contains("password")
            || lower.Contains("secret")
            || lower.Contains("token")
            || lower.Contains("apikey")
            || lower.Contains("privatekey");
    }

    public static bool IsExcludedTable(string? tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return true;
        return ExcludedSystemTables.Contains(tableName.Trim('"'));
    }

    public static Dictionary<string, object?> SanitizeDictionary(IDictionary<string, object?>? rawValues)
    {
        var result = new Dictionary<string, object?>();
        if (rawValues == null) return result;

        foreach (var kvp in rawValues)
        {
            if (IsSensitiveField(kvp.Key))
            {
                result[kvp.Key] = "[REDACTED]";
                continue;
            }

            result[kvp.Key] = NormalizeValue(kvp.Value);
        }

        return result;
    }

    public static string? SerializeSanitized(IDictionary<string, object?>? values)
    {
        if (values == null || values.Count == 0) return null;
        var clean = SanitizeDictionary(values);
        return JsonSerializer.Serialize(clean);
    }

    private static object? NormalizeValue(object? val)
    {
        if (val == null || val == DBNull.Value) return null;
        if (val is DateTime dt) return dt.ToString("O");
        if (val is DateOnly d) return d.ToString("yyyy-MM-dd");
        if (val is Enum e) return e.ToString();
        if (val is byte[] bytes) return $"[binary:{bytes.Length} bytes]";
        return val;
    }
}
