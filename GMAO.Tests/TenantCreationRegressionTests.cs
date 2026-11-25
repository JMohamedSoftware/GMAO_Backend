using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.API.Controllers;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Infrastructure.Audit;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace GMAO.Tests;

public class TenantCreationRegressionTests : IClassFixture<EphemeralPostgresCluster>
{
    private readonly EphemeralPostgresCluster _pgCluster;

    public TenantCreationRegressionTests(EphemeralPostgresCluster pgCluster)
    {
        _pgCluster = pgCluster;
    }

    private sealed class SuperAdminTenantContext : ITenantContextService
    {
        public int? CurrentSocieteId => null;
        public bool IsSuperAdmin => true;
        public bool IsImpersonating => false;
        public int GetRequiredTenantId() => 1;
    }

    private static GmaoDbContext CreateDbContext(
        string connectionString,
        IHttpContextAccessor? httpAccessor = null)
    {
        var interceptor = new AuditSaveChangesInterceptor(
            httpAccessor ?? new HttpContextAccessor(),
            NullLogger<AuditSaveChangesInterceptor>.Instance);

        var options = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new GmaoDbContext(options, new SuperAdminTenantContext());
    }

    private static IHttpContextAccessor CreateSuperAdminHttpAccessor(string? correlationId = null, string? userName = null)
    {
        var ctx = new DefaultHttpContext
        {
            TraceIdentifier = "trace-test-01",
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Role, "SuperAdmin"),
                new Claim("name", userName ?? "Super Admin"),
                new Claim(ClaimTypes.Email, "superadmin@gmao.com")
            }, "TestAuth"))
        };
        ctx.Connection.RemoteIpAddress = IPAddress.Loopback;
        if (!string.IsNullOrEmpty(correlationId))
        {
            ctx.Request.Headers["X-Correlation-Id"] = correlationId;
        }
        return new HttpContextAccessor { HttpContext = ctx };
    }

    private async Task<string> CreateIsolatedDatabaseAsync()
    {
        var dbName = $"gmao_tenant_test_{Guid.NewGuid():N}";
        await using (var conn = new NpgsqlConnection(_pgCluster.ConnectionString))
        {
            await conn.OpenAsync();
            await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\";", conn);
            await createCmd.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_pgCluster.ConnectionString)
        {
            Database = dbName,
            IncludeErrorDetail = true
        };
        var connStr = builder.ConnectionString;

        await using var db = CreateDbContext(connStr);
        var script = db.Database.GenerateCreateScript();
        int firstInsertIdx = script.IndexOf("INSERT INTO \"", StringComparison.Ordinal);
        string ddlPart = firstInsertIdx >= 0 ? script[..firstInsertIdx] : script;
        string seedPart = firstInsertIdx >= 0 ? script[firstInsertIdx..] : string.Empty;

        await using (var conn = new NpgsqlConnection(connStr))
        {
            await conn.OpenAsync();
            await using (var ddlCmd = new NpgsqlCommand(ddlPart, conn))
                await ddlCmd.ExecuteNonQueryAsync();

            await using (var socCmd = new NpgsqlCommand(@"
INSERT INTO ""Societes"" (""Id"", ""CodeTenant"", ""Nom"", ""SubscriptionPlan"", ""SubscriptionStatus"", ""SubscriptionAmount"", ""MaxSites"", ""MaxUsers"", ""MaxEquipements"", ""StorageUsedGb"", ""MaxStorageGb"", ""CapacityTonsPerDay"", ""IsActive"", ""CreatedAt"")
VALUES (1, 'seed-soc', 'Seed Societe', 'Enterprise', 'Actif', 850, 3, 25, 150, 0, 50, 450, TRUE, NOW())
ON CONFLICT (""Id"") DO NOTHING;", conn))
                await socCmd.ExecuteNonQueryAsync();

            if (!string.IsNullOrWhiteSpace(seedPart))
            {
                await using var seedCmd = new NpgsqlCommand(seedPart, conn);
                await seedCmd.ExecuteNonQueryAsync();
            }

            await using (var seqCmd = new NpgsqlCommand(@"
SELECT setval(pg_get_serial_sequence('""Societes""', 'Id'), COALESCE((SELECT MAX(""Id"") FROM ""Societes""), 1));
SELECT setval(pg_get_serial_sequence('""Localisations""', 'Id'), COALESCE((SELECT MAX(""Id"") FROM ""Localisations""), 1));
SELECT setval(pg_get_serial_sequence('""Roles""', 'Id'), COALESCE((SELECT MAX(""Id"") FROM ""Roles""), 1));", conn))
                await seqCmd.ExecuteNonQueryAsync();
        }

        return connStr;
    }

    private static SuperAdminController CreateController(GmaoDbContext db, IHttpContextAccessor httpAccessor)
    {
        var redoMock = new Mock<IRedoLogService>();
        var compMock = new Mock<IBusinessCompensationService>();
        return new SuperAdminController(
            db,
            redoMock.Object,
            compMock.Object,
            NullLogger<SuperAdminController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpAccessor.HttpContext!
            }
        };
    }

    [Fact]
    public async Task CreateTenantWithAdmin_Succeeds_AndCreatesOpenCampaignAndAuditTrail()
    {
        var connStr = await CreateIsolatedDatabaseAsync();
        var accessor = CreateSuperAdminHttpAccessor();
        await using var db = CreateDbContext(connStr, accessor);
        var ctrl = CreateController(db, accessor);

        var dto = new TenantWithAdminDto
        {
            Nom = "Conserverie Test Reg",
            CodeTenant = "conserverie-reg",
            Telephone = "+216 72 100 200",
            Ville = "Nabeul",
            Gouvernorat = "Tunisie",
            AdminPrenom = "Sami",
            AdminNom = "Ben Ali",
            AdminEmail = "sami@conserverie-reg.tn",
            AdminPassword = "Password123!",
            SubscriptionPlan = "Premium",
            SubscriptionAmount = 599,
            IsActive = true
        };

        var result = await ctrl.CreateTenantWithAdmin(dto);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);

        var tenant = await db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CodeTenant == "conserverie-reg");
        Assert.NotNull(tenant);

        var admin = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == "sami@conserverie-reg.tn");
        Assert.NotNull(admin);
        Assert.Equal(tenant!.Id, admin!.SocieteId);
        Assert.Equal(1, admin.RoleId);

        var campagne = await db.Campagnes.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.SocieteId == tenant.Id);
        Assert.NotNull(campagne);
        Assert.Null(campagne!.DateFin);
        Assert.Equal(EtatCampagne.EnCours, campagne.Etat);

        var loc = await db.Localisations.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.SocieteId == tenant.Id);
        Assert.NotNull(loc);

        var socAudit = await db.AuditTrailEntries.FirstOrDefaultAsync(a => a.EntityName == "Societes" && a.EntityId == tenant.Id.ToString());
        Assert.NotNull(socAudit);
        Assert.Equal(tenant.Id, socAudit!.SocieteId);
    }

    [Fact]
    public async Task CreateTenantWithAdmin_HandlesLongCompanyNameAndSlug_WithoutStringLengthCrash()
    {
        var connStr = await CreateIsolatedDatabaseAsync();
        var accessor = CreateSuperAdminHttpAccessor();
        await using var db = CreateDbContext(connStr, accessor);
        var ctrl = CreateController(db, accessor);

        var longName = new string('A', 148);
        var longSlug = new string('b', 75);

        var dto = new TenantWithAdminDto
        {
            Nom = longName,
            CodeTenant = longSlug,
            AdminPrenom = "Ali",
            AdminNom = null, // Falls back to company name (>100 chars -> must safely truncate for User.Nom)
            AdminEmail = "longtest@example.com",
            IsActive = true
        };

        var result = await ctrl.CreateTenantWithAdmin(dto);
        Assert.IsType<OkObjectResult>(result);

        var tenant = await db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Nom == longName);
        Assert.NotNull(tenant);
        Assert.True(tenant!.CodeTenant.Length <= 50);

        var admin = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == "longtest@example.com");
        Assert.NotNull(admin);
        Assert.Equal(100, admin!.Nom.Length);

        var loc = await db.Localisations.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.SocieteId == tenant.Id);
        Assert.NotNull(loc);
        Assert.Equal(150, loc!.Nom.Length);
    }

    [Fact]
    public async Task CreateTenantWithAdmin_RejectsOversizedPhone_WithExplicitFrenchValidationMessage()
    {
        var connStr = await CreateIsolatedDatabaseAsync();
        var accessor = CreateSuperAdminHttpAccessor();
        await using var db = CreateDbContext(connStr, accessor);
        var ctrl = CreateController(db, accessor);

        var dto = new TenantWithAdminDto
        {
            Nom = "Phone Limit Test",
            CodeTenant = "phone-limit-test",
            Telephone = "+216 71 000 000 / 22 000 000", // 29 chars (> 20)
            AdminPrenom = "Nizar",
            AdminNom = "Trabelsi",
            AdminEmail = "nizar@phonelimit.tn",
            IsActive = true
        };

        var result = await ctrl.CreateTenantWithAdmin(dto);
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString() ?? "";
        Assert.Contains("20 caractères", msg);
        Assert.DoesNotContain("inner exception", msg, StringComparison.OrdinalIgnoreCase);
        Assert.False(await db.Societes.IgnoreQueryFilters().AnyAsync(s => s.CodeTenant == "phone-limit-test"));
    }

    [Fact]
    public async Task CreateTenantWithAdmin_WhenPostgresErrorOccurs_UnwrapsInnerExceptionDetails()
    {
        var connStr = await CreateIsolatedDatabaseAsync();
        // Simulate missing AuditTrailEntries table (unmigrated schema scenario)
        await using (var conn = new NpgsqlConnection(connStr))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("DROP TABLE \"AuditTrailEntries\";", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        var accessor = CreateSuperAdminHttpAccessor();
        await using var db = CreateDbContext(connStr, accessor);
        var ctrl = CreateController(db, accessor);

        var dto = new TenantWithAdminDto
        {
            Nom = "Broken Schema Test",
            CodeTenant = "broken-schema-test",
            AdminPrenom = "Test",
            AdminNom = "Admin",
            AdminEmail = "broken@example.com",
            IsActive = true
        };

        var result = await ctrl.CreateTenantWithAdmin(dto);
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var msg = bad.Value?.GetType().GetProperty("message")?.GetValue(bad.Value)?.ToString() ?? "";

        Assert.DoesNotContain("See the inner exception for details", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AuditTrailEntries", msg);
    }

    [Fact]
    public async Task AuditSaveChangesInterceptor_TruncatesOversizedActorMetadataSafely()
    {
        var connStr = await CreateIsolatedDatabaseAsync();
        var longCorr = new string('c', 250);
        var longUser = new string('u', 220);
        var accessor = CreateSuperAdminHttpAccessor(correlationId: longCorr, userName: longUser);

        await using var db = CreateDbContext(connStr, accessor);
        db.Societes.Add(new Societe
        {
            Nom = "Audit Truncate Co",
            CodeTenant = "audit-truncate-co",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var entry = await db.AuditTrailEntries.FirstOrDefaultAsync(a => a.EntityName == "Societes");
        Assert.NotNull(entry);
        Assert.Equal(128, entry!.CorrelationId!.Length);
        Assert.Equal(150, entry.UserName!.Length);
    }
}
