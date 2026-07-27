namespace GMAO.Application.DTOs.Auth;

public class UpdateUserDto
{
    public string? Nom { get; set; }
    public string? Prenom { get; set; }
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public int? RoleId { get; set; }
    public bool? IsActive { get; set; }
    public string? Avatar { get; set; }
}
