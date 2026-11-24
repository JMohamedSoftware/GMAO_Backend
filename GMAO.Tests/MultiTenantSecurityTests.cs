using System;
using System.Security;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;
using GMAO.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GMAO.Tests;

public class MultiTenantSecurityTests
{
    private readonly Mock<ILogger<TenantContextService>> _loggerMock = new();

    private TenantContextService CreateService(ClaimsPrincipal user, IHeaderDictionary? headers = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = user;
        if (headers != null)
        {
            foreach (var h in headers)
            {
                httpContext.Request.Headers[h.Key] = h.Value;
            }
        }

        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        return new TenantContextService(accessorMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void StandardUser_WithTenantClaim_ResolvesCorrectTenantId()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim("SocieteId", "5"),
            new Claim(ClaimTypes.Role, "Technicien")
        }, "TestAuth"));

        var service = CreateService(user);
        var tenantId = service.GetRequiredTenantId();

        Assert.Equal(5, tenantId);
        Assert.False(service.IsSuperAdmin);
    }

    [Fact]
    public void StandardUser_Attempting_XTenantId_HeaderSpoofing_ThrowsSecurityException()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim("SocieteId", "5"),
            new Claim(ClaimTypes.Role, "Technicien")
        }, "TestAuth"));

        var headers = new HeaderDictionary { { "X-Tenant-Id", "99" } };
        var service = CreateService(user, headers);

        // Security check: must immediately reject the forged tenant header
        Assert.Throws<SecurityException>(() => service.GetRequiredTenantId());
    }

    [Fact]
    public void SuperAdmin_WithExplicitTargetTenant_ResolvesImpersonatedTenant()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        }, "TestAuth"));

        var headers = new HeaderDictionary { { "X-Tenant-Id", "7" } };
        var service = CreateService(user, headers);

        Assert.True(service.IsSuperAdmin);
        Assert.True(service.IsImpersonating);
        Assert.Equal(7, service.GetRequiredTenantId());
    }

    [Fact]
    public void TenantContext_Missing_ThrowsUnauthorizedAccessException_FailClosed()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity()); // Unauthenticated
        var service = CreateService(user);

        Assert.Throws<UnauthorizedAccessException>(() => service.GetRequiredTenantId());
        Assert.Null(service.CurrentSocieteId);
    }

    [Fact]
    public async Task GlobalQueryFilter_IsolatesCompany1FromCompany2_FailClosed()
    {
        var options = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseInMemoryDatabase(databaseName: $"GmaoTestDb_{Guid.NewGuid()}")
            .Options;

        // 1. Seed as SuperAdmin (authorized to create cross-tenant records)
        var adminTenantServiceMock = new Mock<ITenantContextService>();
        adminTenantServiceMock.Setup(t => t.IsSuperAdmin).Returns(true);
        adminTenantServiceMock.Setup(t => t.IsImpersonating).Returns(false);

        using (var db = new GmaoDbContext(options, adminTenantServiceMock.Object))
        {
            db.Equipements.AddRange(
                new Equipement { Code = "EQ-T1", Designation = "Pompe Tenant 1", SocieteId = 1 },
                new Equipement { Code = "EQ-T2", Designation = "Chaudière Tenant 2", SocieteId = 2 }
            );
            await db.SaveChangesAsync();
        }

        // 2. Query through standard Tenant 1 context
        var tenant1ServiceMock = new Mock<ITenantContextService>();
        tenant1ServiceMock.Setup(t => t.CurrentSocieteId).Returns(1);
        tenant1ServiceMock.Setup(t => t.IsSuperAdmin).Returns(false);
        tenant1ServiceMock.Setup(t => t.IsImpersonating).Returns(false);

        using (var db = new GmaoDbContext(options, tenant1ServiceMock.Object))
        {
            var visibleEquipments = await db.Equipements.ToListAsync();

            // Only Tenant 1 equipment should be visible
            Assert.Single(visibleEquipments);
            Assert.Equal("EQ-T1", visibleEquipments[0].Code);
            Assert.Equal(1, visibleEquipments[0].SocieteId);
        }
    }

    [Fact]
    public async Task SaveChanges_PreventsTamperingWithSocieteId_ThrowsSecurityException()
    {
        var options = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseInMemoryDatabase(databaseName: $"GmaoTestDb_Tamper_{Guid.NewGuid()}")
            .Options;

        var tenantServiceMock = new Mock<ITenantContextService>();
        tenantServiceMock.Setup(t => t.CurrentSocieteId).Returns(1);
        tenantServiceMock.Setup(t => t.GetRequiredTenantId()).Returns(1);
        tenantServiceMock.Setup(t => t.IsSuperAdmin).Returns(false);
        tenantServiceMock.Setup(t => t.IsImpersonating).Returns(false);

        using (var db = new GmaoDbContext(options, tenantServiceMock.Object))
        {
            var eq = new Equipement { Code = "EQ-T1", Designation = "Pompe", SocieteId = 1 };
            db.Equipements.Add(eq);
            await db.SaveChangesAsync();

            // Attempt to maliciously modify SocieteId to 2
            eq.SocieteId = 2;
            await Assert.ThrowsAsync<SecurityException>(() => db.SaveChangesAsync());
        }
    }
}
