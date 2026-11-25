using System;
using System.Security.Cryptography;
using System.Text;

namespace GMAO.Application.Services;

public static class PasswordHasher
{
    public static string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Le mot de passe ne peut pas être vide.", nameof(password));

        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public static bool IsBcryptHash(string? hash) =>
        !string.IsNullOrEmpty(hash) && hash.StartsWith("$2", StringComparison.Ordinal);

    public static bool IsSha256HexHash(string? hash)
    {
        if (string.IsNullOrEmpty(hash) || hash.Length != 64)
            return false;

        foreach (char c in hash)
        {
            bool isHex = (c >= '0' && c <= '9') ||
                         (c >= 'a' && c <= 'f') ||
                         (c >= 'A' && c <= 'F');
            if (!isHex) return false;
        }
        return true;
    }

    public static bool NeedsRehash(string? hash) => !IsBcryptHash(hash);

    public static bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password))
            return false;

        // 1. Vérification BCrypt ($2a$, $2b$, $2y$, etc.)
        if (IsBcryptHash(hash))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch
            {
                return false;
            }
        }

        // 2. Support transitoire SHA-256 hex pour les comptes seedés historiques (rehashés en BCrypt à la connexion)
        if (IsSha256HexHash(hash))
        {
            var shaBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var shaHex = Convert.ToHexString(shaBytes);
            return shaHex.Equals(hash, StringComparison.OrdinalIgnoreCase);
        }

        // Aucun fallback en texte clair n'est accepté.
        return false;
    }
}
