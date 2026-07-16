namespace GMAO.Domain.Entities;

/// <summary>
/// Gérer les tokens JWT refresh pour l'authentification sécurisée
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; } = false;
    public string? ReplacedByToken { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
