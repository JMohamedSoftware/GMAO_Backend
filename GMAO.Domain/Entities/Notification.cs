using GMAO.Domain.Enums;

namespace GMAO.Domain.Entities;

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Titre { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public TypeNotification Type { get; set; }
    public bool Lu { get; set; } = false;
    public DateTime? LuAt { get; set; }
    public string? LienUrl { get; set; }    // Lien de redirection
    public DateTime Date { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
