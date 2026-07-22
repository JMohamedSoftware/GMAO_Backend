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
        var dbContext = context.HttpContext.RequestServices.GetService<GmaoDbContext>();
        
        var userIdString = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (dbContext == null)
        {
             context.Result = new StatusCodeResult(500);
             return;
        }

        var user = await dbContext.Users
            .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || user.Role == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // SuperAdmin can do everything
        if (user.Role.Nom == "SuperAdmin")
        {
            await next();
            return;
        }

        var hasPermission = user.Role.RolePermissions.Any(rp => rp.PermissionName == _permission);
        if (!hasPermission)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
