using System.Security.Claims;

namespace GMAO.Domain.Constants;

public static class GmaoRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Administrateur = "Administrateur";
    public const string ResponsableMaintenance = "Responsable Maintenance";
    public const string ChefEquipe = "Chef d'équipe";
    public const string Technicien = "Technicien";
    public const string ResponsableProduction = "Responsable Production";

    public const int AdministrateurId = 1;
    public const int ResponsableMaintenanceId = 2;
    public const int ChefEquipeId = 3;
    public const int TechnicienId = 4;
    public const int ResponsableProductionId = 5;
    public const int SuperAdminId = 7;

    public static bool IsSuperAdmin(ClaimsPrincipal user) => user.IsInRole(SuperAdmin);

    public static bool IsCompanyAdmin(ClaimsPrincipal user) =>
        user.IsInRole(SuperAdmin) || user.IsInRole(Administrateur);

    public static bool IsManagerial(ClaimsPrincipal user) =>
        user.IsInRole(SuperAdmin) ||
        user.IsInRole(Administrateur) ||
        user.IsInRole(ResponsableMaintenance);

    public static bool IsValidTenantAssignableRoleId(int roleId) =>
        roleId is AdministrateurId
            or ResponsableMaintenanceId
            or ChefEquipeId
            or TechnicienId
            or ResponsableProductionId;
}
