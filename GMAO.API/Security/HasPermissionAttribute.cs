using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GMAO.API.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class HasPermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _permission;

    public HasPermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // SuperAdmin and Admin can do everything
        if (user.HasClaim(ClaimTypes.Role, "SuperAdmin") || user.IsInRole("SuperAdmin") ||
            user.HasClaim(ClaimTypes.Role, "Admin") || user.IsInRole("Admin"))
        {
            await next();
            return;
        }

        var hasPermission = user.HasClaim("Permission", _permission);
        if (!hasPermission)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
