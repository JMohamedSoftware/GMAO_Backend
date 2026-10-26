using System;
using System.Security.Cryptography;
using System.Text;

namespace GMAO.Application.Services;

public static class PasswordHasher
{
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public static bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password))
            return false;

        // 1. BCrypt verification ($2a$, $2b$, $2y$, etc.)
        if (hash.StartsWith("$2"))
        {
            try
            {
                if (BCrypt.Net.BCrypt.Verify(password, hash))
                    return true;
            }
            catch
            {
                // Fallback to other checks
            }
        }

        // 2. SHA256 hex verification
        var shaBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var shaHex = Convert.ToHexString(shaBytes).ToLower();
        if (shaHex.Equals(hash, StringComparison.OrdinalIgnoreCase))
            return true;

        // 3. Plaintext fallback for dev / initial seed accounts
        if (password == hash)
            return true;

        return false;
    }
}
