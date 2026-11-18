namespace GMAO.Domain.Entities;

public class RolePermission
{
    public int Id { get; set; }
    
    // Multi-tenant: null pour les permissions par défaut système, societeId pour les permissions spécifiques à l'entreprise
    public int? SocieteId { get; set; }
    public Societe? Societe { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public string PermissionName { get; set; } = string.Empty;
}
