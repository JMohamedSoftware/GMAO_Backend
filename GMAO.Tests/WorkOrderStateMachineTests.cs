using System;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.API.Controllers;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GMAO.Tests;

public class WorkOrderStateMachineTests
{
    private (OrdresTravailController controller, GmaoDbContext db) CreateController(
        ClaimsPrincipal user, 
        int tenantId = 1)
    {
        var options = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseInMemoryDatabase(databaseName: $"GmaoTestDb_WO_{Guid.NewGuid()}")
            .Options;

        var tenantServiceMock = new Mock<ITenantContextService>();
        tenantServiceMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);
        tenantServiceMock.Setup(t => t.CurrentSocieteId).Returns(tenantId);
        tenantServiceMock.Setup(t => t.IsSuperAdmin).Returns(user.IsInRole("SuperAdmin"));
        tenantServiceMock.Setup(t => t.IsImpersonating).Returns(false);

        var db = new GmaoDbContext(options, tenantServiceMock.Object);

        var repoMock = new Mock<IGenericRepository<OrdresTravail>>();
        var incidentRepoMock = new Mock<IGenericRepository<DemandeIntervention>>();

        var controller = new OrdresTravailController(repoMock.Object, incidentRepoMock.Object, db, tenantServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return (controller, db);
    }

    [Fact]
    public async Task Update_FromCreeToPlanifie_Succeeds()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim(ClaimTypes.Role, "ChefEquipe"),
            new Claim("SocieteId", "1")
        }, "TestAuth"));

        var (controller, db) = CreateController(user);

        var wo = new OrdresTravail
        {
            Id = 1,
            NumeroOT = "OT-2026-001",
            SocieteId = 1,
            Statut = StatutOT.Cree,
            Description = "Test Intervention"
        };
        db.OrdresTravail.Add(wo);
        await db.SaveChangesAsync();

        var updateDto = new OrdresTravail
        {
            Id = 1,
            Statut = StatutOT.Planifie
        };

        var result = await controller.Update(1, updateDto);
        Assert.IsType<NoContentResult>(result);

        var updated = await db.OrdresTravail.FindAsync(1);
        Assert.Equal(StatutOT.Planifie, updated!.Statut);
    }

    [Fact]
    public async Task Update_TransitionToCloture_ByTechnician_ReturnsForbidden()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "20"),
            new Claim(ClaimTypes.Role, "Technicien"),
            new Claim("SocieteId", "1")
        }, "TestAuth"));

        var (controller, db) = CreateController(user);

        var wo = new OrdresTravail
        {
            Id = 2,
            NumeroOT = "OT-2026-002",
            SocieteId = 1,
            Statut = StatutOT.Termine,
            Description = "Travail terminé"
        };
        db.OrdresTravail.Add(wo);
        await db.SaveChangesAsync();

        // Technician attempts to close the work order
        var updateDto = new OrdresTravail
        {
            Id = 2,
            Statut = StatutOT.Cloture
        };

        var result = await controller.Update(2, updateDto);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Update_TransitionToCloture_ByResponsable_Succeeds()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "5"),
            new Claim(ClaimTypes.Role, "ResponsableMaintenance"),
            new Claim("SocieteId", "1")
        }, "TestAuth"));

        var (controller, db) = CreateController(user);

        var wo = new OrdresTravail
        {
            Id = 3,
            NumeroOT = "OT-2026-003",
            SocieteId = 1,
            Statut = StatutOT.Termine,
            Description = "En attente de clôture"
        };
        db.OrdresTravail.Add(wo);
        await db.SaveChangesAsync();

        var updateDto = new OrdresTravail
        {
            Id = 3,
            Statut = StatutOT.Cloture
        };

        var result = await controller.Update(3, updateDto);
        Assert.IsType<NoContentResult>(result);

        var updated = await db.OrdresTravail.FindAsync(3);
        Assert.Equal(StatutOT.Cloture, updated!.Statut);
    }

    [Fact]
    public async Task Update_MutationFromCloture_ReturnsConflict()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "5"),
            new Claim(ClaimTypes.Role, "ResponsableMaintenance"),
            new Claim("SocieteId", "1")
        }, "TestAuth"));

        var (controller, db) = CreateController(user);

        var wo = new OrdresTravail
        {
            Id = 4,
            NumeroOT = "OT-2026-004",
            SocieteId = 1,
            Statut = StatutOT.Cloture,
            Description = "Clôturé"
        };
        db.OrdresTravail.Add(wo);
        await db.SaveChangesAsync();

        // Attempting to change status of an already closed work order
        var updateDto = new OrdresTravail
        {
            Id = 4,
            Statut = StatutOT.EnCours
        };

        var result = await controller.Update(4, updateDto);
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("clôturé", conflictResult.Value!.ToString()!, StringComparison.OrdinalIgnoreCase);
    }
}
