using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.API.Controllers;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GMAO.Tests;

public class RbacScopingTests
{
    private (IncidentsController controller, GmaoDbContext db) CreateIncidentsController(
        DbContextOptions<GmaoDbContext> options,
        ClaimsPrincipal user)
    {
        var tenantServiceMock = new Mock<ITenantContextService>();
        tenantServiceMock.Setup(t => t.GetRequiredTenantId()).Returns(1);
        tenantServiceMock.Setup(t => t.CurrentSocieteId).Returns(1);
        tenantServiceMock.Setup(t => t.IsSuperAdmin).Returns(user.IsInRole("SuperAdmin"));
        tenantServiceMock.Setup(t => t.IsImpersonating).Returns(false);

        var db = new GmaoDbContext(options, tenantServiceMock.Object);

        var repoMock = new Mock<IGenericRepository<DemandeIntervention>>();

        var controller = new IncidentsController(repoMock.Object, db, tenantServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return (controller, db);
    }

    [Fact]
    public async Task Incidents_GetAll_WithOwnScope_ReturnsOnlyUserIncidents()
    {
        var options = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseInMemoryDatabase(databaseName: $"GmaoTestDb_Incidents_All_{System.Guid.NewGuid()}")
            .Options;

        // User has only INCIDENTS_VIEW_OWN
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim(ClaimTypes.Role, "Technicien"),
            new Claim("SocieteId", "1"),
            new Claim("Permission", "INCIDENTS_VIEW_OWN")
        }, "TestAuth"));

        var (controller, db) = CreateIncidentsController(options, user);

        // Seed 2 incidents: one by user 10, one by user 20
        var inc1 = new DemandeIntervention { SocieteId = 1, DemandeurId = 10, Description = "Panne Pompe 1" };
        var inc2 = new DemandeIntervention { SocieteId = 1, DemandeurId = 20, Description = "Panne Chaudière 2" };
        db.DemandesIntervention.AddRange(inc1, inc2);
        await db.SaveChangesAsync();

        var result = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<DemandeIntervention>>(okResult.Value);

        Assert.Single(list);
        Assert.Contains(list, i => i.DemandeurId == 10);
    }

    [Fact]
    public async Task Incidents_GetById_WithOwnScope_ForeignIncident_ReturnsForbidden()
    {
        var options = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseInMemoryDatabase(databaseName: $"GmaoTestDb_Incidents_ById_{System.Guid.NewGuid()}")
            .Options;

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim(ClaimTypes.Role, "Technicien"),
            new Claim("SocieteId", "1"),
            new Claim("Permission", "INCIDENTS_VIEW_OWN")
        }, "TestAuth"));

        var (controller, db) = CreateIncidentsController(options, user);

        var foreignUser = new User
        {
            Id = 99,
            Email = "foreign@test.com",
            Nom = "Foreign",
            Prenom = "User",
            PasswordHash = "hash",
            SocieteId = 1,
            RoleId = 4
        };
        db.Users.Add(foreignUser);

        var foreignIncident = new DemandeIntervention 
        { 
            SocieteId = 1, 
            DemandeurId = foreignUser.Id, 
            Description = "Incident Autre Utilisateur" 
        };
        db.DemandesIntervention.Add(foreignIncident);
        await db.SaveChangesAsync();

        var result = await controller.GetById(foreignIncident.Id);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Incidents_GetAll_WithAdminRole_ReturnsAllCompanyIncidents()
    {
        var options = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseInMemoryDatabase(databaseName: $"GmaoTestDb_Incidents_Admin_{System.Guid.NewGuid()}")
            .Options;

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Administrateur"),
            new Claim("SocieteId", "1")
        }, "TestAuth"));

        var (controller, db) = CreateIncidentsController(options, user);

        db.DemandesIntervention.AddRange(
            new DemandeIntervention { SocieteId = 1, DemandeurId = 10, Description = "Panne 1" },
            new DemandeIntervention { SocieteId = 1, DemandeurId = 20, Description = "Panne 2" }
        );
        await db.SaveChangesAsync();

        var result = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<DemandeIntervention>>(okResult.Value);

        Assert.Equal(2, System.Linq.Enumerable.Count(list));
    }
}
