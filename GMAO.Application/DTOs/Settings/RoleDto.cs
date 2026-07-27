namespace GMAO.Application.DTOs.Settings;

public class RoleDto
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new List<string>();
}
