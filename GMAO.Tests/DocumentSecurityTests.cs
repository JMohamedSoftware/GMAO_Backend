using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.API.Controllers;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GMAO.Tests;

public class DocumentSecurityTests
{
    private (DocumentController controller, GmaoDbContext db, Mock<IFileStorageService> storageMock) CreateController(
        DbContextOptions<GmaoDbContext> options,
        ClaimsPrincipal user, 
        int tenantId = 1)
    {
        var tenantServiceMock = new Mock<ITenantContextService>();
        tenantServiceMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);
        tenantServiceMock.Setup(t => t.CurrentSocieteId).Returns(tenantId);
        tenantServiceMock.Setup(t => t.IsSuperAdmin).Returns(user.IsInRole("SuperAdmin"));
        tenantServiceMock.Setup(t => t.IsImpersonating).Returns(false);

        var db = new GmaoDbContext(options, tenantServiceMock.Object);
        var storageMock = new Mock<IFileStorageService>();

        var controller = new DocumentController(db, storageMock.Object, tenantServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return (controller, db, storageMock);
    }

    [Fact]
    public async Task GetFile_CrossTenantDocument_ReturnsForbidden()
    {
        var options = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseInMemoryDatabase(databaseName: $"GmaoTestDb_Docs_Get_{System.Guid.NewGuid()}")
            .Options;

        // 1. Seed Tenant 2 data using SuperAdmin context
        var adminTenantServiceMock = new Mock<ITenantContextService>();
        adminTenantServiceMock.Setup(t => t.IsSuperAdmin).Returns(true);
        adminTenantServiceMock.Setup(t => t.IsImpersonating).Returns(false);

        var docTenant2 = new Document
        {
            Nom = "Schema_Confidentiel.pdf",
            Chemin = "uploads/tenants/2/schema.pdf",
            UploadedById = 99
        };

        using (var seedDb = new GmaoDbContext(options, adminTenantServiceMock.Object))
        {
            var eqTenant2 = new Equipement { SocieteId = 2, Code = "EQ-T2", Designation = "Chaudière" };
            seedDb.Equipements.Add(eqTenant2);
            await seedDb.SaveChangesAsync();

            docTenant2.EquipementId = eqTenant2.Id;
            seedDb.Documents.Add(docTenant2);
            await seedDb.SaveChangesAsync();
        }

        // 2. Authenticated user belonging to Tenant 1 attempts to download
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim("SocieteId", "1")
        }, "TestAuth"));

        var (controller, db, storageMock) = CreateController(options, user, tenantId: 1);

        var result = await controller.GetFile(docTenant2.Id);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Delete_CrossTenantDocument_ReturnsForbidden()
    {
        var options = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseInMemoryDatabase(databaseName: $"GmaoTestDb_Docs_Del_{System.Guid.NewGuid()}")
            .Options;

        // 1. Seed Tenant 2 data using SuperAdmin context
        var adminTenantServiceMock = new Mock<ITenantContextService>();
        adminTenantServiceMock.Setup(t => t.IsSuperAdmin).Returns(true);
        adminTenantServiceMock.Setup(t => t.IsImpersonating).Returns(false);

        var docTenant2 = new Document
        {
            Nom = "Rapport_Audit.pdf",
            Chemin = "uploads/tenants/2/rapport.pdf",
            UploadedById = 99
        };

        using (var seedDb = new GmaoDbContext(options, adminTenantServiceMock.Object))
        {
            var eqTenant2 = new Equipement { SocieteId = 2, Code = "EQ-T2-B", Designation = "Moteur" };
            seedDb.Equipements.Add(eqTenant2);
            await seedDb.SaveChangesAsync();

            docTenant2.EquipementId = eqTenant2.Id;
            seedDb.Documents.Add(docTenant2);
            await seedDb.SaveChangesAsync();
        }

        // 2. Authenticated user belonging to Tenant 1 attempts to delete
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim("SocieteId", "1")
        }, "TestAuth"));

        var (controller, db, storageMock) = CreateController(options, user, tenantId: 1);

        var result = await controller.Delete(docTenant2.Id);
        Assert.IsType<ForbidResult>(result);
    }
}
