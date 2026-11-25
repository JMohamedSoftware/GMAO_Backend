using System.Security.Claims;
using GMAO.Domain.Constants;

namespace GMAO.Application.Security;

public enum AccessScope
{
    None = 0,
    Own = 1,
    Team = 2,
    All = 3
}

public static class GmaoPermissions
{
    public const string PermissionsLoadedClaim = "PermissionsLoaded";

    // Dashboard
    public const string DashboardView = "DASHBOARD_VIEW";

    // Ordres de travail
    public const string WorkOrderView = "WORKORDER_VIEW";
    public const string WorkOrderCreate = "WORKORDER_CREATE";
    public const string WorkOrderUpdate = "WORKORDER_UPDATE";
    public const string WorkOrderDelete = "WORKORDER_DELETE";
    public const string WorkOrderAssign = "WORKORDER_ASSIGN";
    public const string WorkOrderClose = "WORKORDER_CLOSE";
    public const string WorkOrderStart = "WORKORDER_START";
    public const string WorkOrderSuspend = "WORKORDER_SUSPEND";
    public const string WorkOrderFinish = "WORKORDER_FINISH";
    public const string WorkOrderExecute = "WORKORDER_EXECUTE";
    public const string WorkOrderExport = "WORKORDER_EXPORT";

    // Equipements
    public const string EquipmentView = "EQUIPMENT_VIEW";
    public const string EquipmentCreate = "EQUIPMENT_CREATE";
    public const string EquipmentUpdate = "EQUIPMENT_UPDATE";
    public const string EquipmentDelete = "EQUIPMENT_DELETE";
    public const string EquipmentImport = "EQUIPMENT_IMPORT";
    public const string EquipmentExport = "EQUIPMENT_EXPORT";

    // Utilisateurs
    public const string UserView = "USER_VIEW";
    public const string UserCreate = "USER_CREATE";
    public const string UserUpdate = "USER_UPDATE";
    public const string UserDelete = "USER_DELETE";

    // Maintenance preventive
    public const string PreventiveView = "PREVENTIVE_VIEW";
    public const string PreventiveCreate = "PREVENTIVE_CREATE";
    public const string PreventiveUpdate = "PREVENTIVE_UPDATE";
    public const string PreventiveDelete = "PREVENTIVE_DELETE";
    public const string PreventiveExecute = "PREVENTIVE_EXECUTE";

    // Incidents (Pannes)
    public const string IncidentView = "INCIDENT_VIEW";
    public const string IncidentCreate = "INCIDENT_CREATE";
    public const string IncidentUpdate = "INCIDENT_UPDATE";
    public const string IncidentDelete = "INCIDENT_DELETE";
    public const string IncidentValidate = "INCIDENT_VALIDATE";

    // Fournisseurs
    public const string SupplierView = "SUPPLIER_VIEW";
    public const string SupplierCreate = "SUPPLIER_CREATE";
    public const string SupplierUpdate = "SUPPLIER_UPDATE";
    public const string SupplierDelete = "SUPPLIER_DELETE";

    // Stock & pieces
    public const string InventoryView = "INVENTORY_VIEW";
    public const string InventoryCreate = "INVENTORY_CREATE";
    public const string InventoryUpdate = "INVENTORY_UPDATE";
    public const string InventoryDelete = "INVENTORY_DELETE";
    public const string InventoryMove = "INVENTORY_MOVE";

    // Rapports
    public const string ReportView = "REPORT_VIEW";
    public const string ReportCreate = "REPORT_CREATE";
    public const string ReportExport = "REPORT_EXPORT";

    private static readonly IReadOnlyList<string> AllAdminPermissions = new[]
    {
        DashboardView,
        $"{WorkOrderView}_ALL", $"{WorkOrderCreate}_ALL", $"{WorkOrderUpdate}_ALL", WorkOrderDelete,
        WorkOrderAssign, WorkOrderClose, WorkOrderStart, WorkOrderSuspend, WorkOrderFinish, WorkOrderExecute, WorkOrderExport,
        EquipmentView, EquipmentCreate, EquipmentUpdate, EquipmentDelete, EquipmentImport, EquipmentExport,
        UserView, UserCreate, UserUpdate, UserDelete,
        $"{PreventiveView}_ALL", PreventiveCreate, PreventiveUpdate, PreventiveDelete, PreventiveExecute,
        $"{IncidentView}_ALL", $"{IncidentCreate}_ALL", $"{IncidentUpdate}_ALL", IncidentDelete, IncidentValidate,
        SupplierView, SupplierCreate, SupplierUpdate, SupplierDelete,
        InventoryView, InventoryCreate, InventoryUpdate, InventoryDelete, InventoryMove,
        ReportView, ReportCreate, ReportExport
    };

    private static readonly IReadOnlyList<string> ResponsableMaintenanceDefaults = new[]
    {
        DashboardView,
        $"{WorkOrderView}_ALL", $"{WorkOrderCreate}_ALL", $"{WorkOrderUpdate}_ALL",
        WorkOrderAssign, WorkOrderStart, WorkOrderSuspend, WorkOrderFinish, WorkOrderClose, WorkOrderExecute, WorkOrderExport,
        EquipmentView, EquipmentCreate, EquipmentUpdate, EquipmentImport, EquipmentExport,
        $"{IncidentView}_ALL", $"{IncidentCreate}_ALL", $"{IncidentUpdate}_ALL", IncidentValidate,
        $"{PreventiveView}_ALL", PreventiveCreate, PreventiveUpdate, PreventiveExecute,
        InventoryView, InventoryCreate, InventoryUpdate, InventoryMove,
        SupplierView,
        UserView,
        ReportView, ReportCreate, ReportExport
    };

    private static readonly IReadOnlyList<string> ChefEquipeDefaults = new[]
    {
        DashboardView,
        $"{WorkOrderView}_TEAM", $"{WorkOrderCreate}_TEAM", $"{WorkOrderUpdate}_TEAM",
        WorkOrderAssign, WorkOrderStart, WorkOrderSuspend, WorkOrderFinish, WorkOrderClose, WorkOrderExecute,
        EquipmentView, EquipmentUpdate,
        $"{IncidentView}_TEAM", $"{IncidentCreate}_TEAM", IncidentValidate,
        $"{PreventiveView}_TEAM", PreventiveCreate, PreventiveUpdate, PreventiveExecute,
        InventoryView, InventoryMove,
        SupplierView,
        UserView,
        ReportView, ReportExport
    };

    private static readonly IReadOnlyList<string> TechnicienDefaults = new[]
    {
        DashboardView,
        $"{WorkOrderView}_OWN", $"{WorkOrderUpdate}_OWN",
        WorkOrderStart, WorkOrderSuspend, WorkOrderFinish, WorkOrderExecute,
        EquipmentView,
        $"{IncidentView}_OWN", $"{IncidentCreate}_OWN",
        $"{PreventiveView}_OWN", PreventiveExecute,
        InventoryView
    };

    private static readonly IReadOnlyList<string> ResponsableProductionDefaults = new[]
    {
        DashboardView,
        EquipmentView,
        $"{IncidentView}_OWN", $"{IncidentCreate}_OWN"
    };

    public static IReadOnlyList<string> GetDefaultPermissionsForRole(string? roleName) => roleName switch
    {
        GmaoRoles.SuperAdmin => AllAdminPermissions,
        GmaoRoles.Administrateur => AllAdminPermissions,
        GmaoRoles.ResponsableMaintenance => ResponsableMaintenanceDefaults,
        GmaoRoles.ChefEquipe => ChefEquipeDefaults,
        GmaoRoles.Technicien => TechnicienDefaults,
        GmaoRoles.ResponsableProduction => ResponsableProductionDefaults,
        _ => Array.Empty<string>()
    };

    public static IReadOnlyCollection<string> GetEffectiveClaimPermissions(ClaimsPrincipal user)
    {
        var explicitPerms = user.FindAll("Permission")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool permissionsLoaded = user.HasClaim(PermissionsLoadedClaim, "true") || explicitPerms.Count > 0;
        if (permissionsLoaded)
            return explicitPerms;

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value);
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            foreach (var p in GetDefaultPermissionsForRole(role))
                resolved.Add(p);
        }
        return resolved;
    }

    public static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        if (GmaoRoles.IsCompanyAdmin(user))
            return true;

        var effective = GetEffectiveClaimPermissions(user);
        foreach (var alias in ExpandAliases(permission))
        {
            if (effective.Contains(alias) ||
                effective.Contains($"{alias}_ALL") ||
                effective.Contains($"{alias}_TEAM") ||
                effective.Contains($"{alias}_OWN"))
            {
                return true;
            }
        }

        return false;
    }

    public static AccessScope GetScope(ClaimsPrincipal user, string basePermission)
    {
        if (user.Identity?.IsAuthenticated != true)
            return AccessScope.None;

        if (GmaoRoles.IsCompanyAdmin(user))
            return AccessScope.All;

        var effective = GetEffectiveClaimPermissions(user);
        foreach (var alias in ExpandAliases(basePermission))
        {
            if (effective.Contains($"{alias}_ALL") || effective.Contains(alias))
                return AccessScope.All;
        }
        foreach (var alias in ExpandAliases(basePermission))
        {
            if (effective.Contains($"{alias}_TEAM"))
                return AccessScope.Team;
        }
        foreach (var alias in ExpandAliases(basePermission))
        {
            if (effective.Contains($"{alias}_OWN"))
                return AccessScope.Own;
        }

        return AccessScope.None;
    }

    private static IEnumerable<string> ExpandAliases(string permission)
    {
        yield return permission;

        // Backward compatibility between canonical INCIDENT_* and legacy INCIDENTS_*
        if (permission.StartsWith("INCIDENT_", StringComparison.OrdinalIgnoreCase))
        {
            yield return "INCIDENTS_" + permission["INCIDENT_".Length..];
        }
        else if (permission.StartsWith("INCIDENTS_", StringComparison.OrdinalIgnoreCase))
        {
            yield return "INCIDENT_" + permission["INCIDENTS_".Length..];
        }
    }
}
