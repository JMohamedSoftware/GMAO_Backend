namespace GMAO.Application.DTOs.Auth;

public class UserProfileDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? TenantName { get; set; }
    public bool IsActive { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new List<string>();
}
