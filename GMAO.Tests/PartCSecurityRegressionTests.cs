using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.API.Controllers;
using GMAO.Application.DTOs.Auth;
using GMAO.Application.DTOs.Equipes;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GMAO.Tests;

public class PartCSecurityRegressionTests
{
    [Theory]
    [InlineData(typeof(SuperAdminController))]
    [InlineData(typeof(DiagnosticController))]
    public void CriticalControllers_MustRequire_SuperAdminRole_AndNeverAllowAnonymous(Type controllerType)
    {
        var allowAnon = controllerType.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.Null(allowAnon);

        var authAttr = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal("SuperAdmin", authAttr!.Roles);

        foreach (var method in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
        }
    }

    [Fact]
    public async Task UsersController_StandardAdmin_CannotSeeCrossTenantOrSuperAdmin_AndCannotElevateToRole7()
    {
        var userRepoMock = new Mock<IUserRepository>();
        var allUsers = new List<User>
        {
            new() { Id = 1, Nom = "SA", Email = "sa@gmao.io", RoleId = 7, Role = new Role { Id = 7, Nom = "SuperAdmin" }, SocieteId = null },
            new() { Id = 2, Nom = "Admin T1", Email = "a1@t1.io", RoleId = 1, Role = new Role { Id = 1, Nom = "Administrateur" }, SocieteId = 1 },
            new() { Id = 3, Nom = "Orphan", Email = "o@null.io", RoleId = 5, Role = new Role { Id = 5, Nom = "Technicien" }, SocieteId = null },
            new() { Id = 4, Nom = "Tech T2", Email = "t2@t2.io", RoleId = 5, Role = new Role { Id = 5, Nom = "Technicien" }, SocieteId = 2 }
        };
        userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(allUsers);
        userRepoMock.Setup(r => r.GetByIdAsync(4)).ReturnsAsync(allUsers[3]);

        var authMock = new Mock<IAuthService>();
        var tenantMock = new Mock<ITenantContextService>();
        tenantMock.Setup(t => t.IsSuperAdmin).Returns(false);
        tenantMock.Setup(t => t.CurrentSocieteId).Returns(1);
        tenantMock.Setup(t => t.GetRequiredTenantId()).Returns(1);

        var ctrl = new UsersController(authMock.Object, userRepoMock.Object, tenantMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "2"),
                        new Claim("SocieteId", "1"),
                        new Claim(ClaimTypes.Role, "Administrateur")
                    }, "TestAuth"))
                }
            }
        };

        // 1. GetAllUsers : ne doit voir QUE Tenant 1 (jamais null SocieteId, SuperAdmin, ou Tenant 2)
        var listRes = await ctrl.GetAllUsers();
        var okRes = Assert.IsType<OkObjectResult>(listRes);
        var items = Assert.IsAssignableFrom<IEnumerable<object>>(okRes.Value).ToList();
        Assert.Single(items);

        // 2. CreateUser vers RoleId 7 (SuperAdmin) -> 403 Forbid
        var createRes = await ctrl.CreateUser(new RegisterDto
        {
            User = new User
            {
                Email = "evil@t1.io",
                Nom = "Evil",
                Prenom = "User",
                RoleId = 7
            },
            Password = "Password123!"
        });
        Assert.IsType<ForbidResult>(createRes);

        // 3. DeleteUser d'un utilisateur du Tenant 2 -> 403 Forbid
        var delCrossRes = await ctrl.DeleteUser(4);
        Assert.IsType<ForbidResult>(delCrossRes);
    }

    [Fact]
    public async Task EquipesController_CrossTenantGetAndDelete_AreBlocked()
    {
        var equipeServiceMock = new Mock<IEquipeService>();
        equipeServiceMock.Setup(s => s.GetEquipeByIdAsync(99)).ReturnsAsync(new EquipeDto
        {
            Id = 99,
            SocieteId = 2,
            Nom = "Equipe Tenant 2"
        });

        var tenantMock = new Mock<ITenantContextService>();
        tenantMock.Setup(t => t.IsSuperAdmin).Returns(false);
        tenantMock.Setup(t => t.CurrentSocieteId).Returns(1);
        tenantMock.Setup(t => t.GetRequiredTenantId()).Returns(1);

        var ctrl = new EquipesController(equipeServiceMock.Object, tenantMock.Object);

        var getCross = await ctrl.GetEquipe(99);
        Assert.IsType<NotFoundResult>(getCross);

        var delCross = await ctrl.DeleteEquipe(99);
        Assert.IsType<NotFoundResult>(delCross);
    }

    [Fact]
    public async Task LocalisationsController_CrossTenantGetByIdAndDelete_ReturnForbid()
    {
        var dbOpts = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseInMemoryDatabase($"LocSecTest_{Guid.NewGuid()}")
            .Options;

        var adminTenant = new Mock<ITenantContextService>();
        adminTenant.Setup(t => t.IsSuperAdmin).Returns(true);

        var locServiceMock = new Mock<ILocalisationService>();
        locServiceMock.Setup(s => s.GetByIdAsync(50)).ReturnsAsync(new Localisation
        {
            Id = 50,
            SocieteId = 2,
            Nom = "Atelier Tenant 2"
        });

        var tenant1Mock = new Mock<ITenantContextService>();
        tenant1Mock.Setup(t => t.IsSuperAdmin).Returns(false);
        tenant1Mock.Setup(t => t.CurrentSocieteId).Returns(1);
        tenant1Mock.Setup(t => t.GetRequiredTenantId()).Returns(1);

        await using var db = new GmaoDbContext(dbOpts, tenant1Mock.Object);
        var ctrl = new LocalisationsController(locServiceMock.Object, db, tenant1Mock.Object);

        var getRes = await ctrl.GetById(50);
        Assert.IsType<ForbidResult>(getRes.Result);

        var delRes = await ctrl.Delete(50);
        Assert.IsType<ForbidResult>(delRes);
    }
}
