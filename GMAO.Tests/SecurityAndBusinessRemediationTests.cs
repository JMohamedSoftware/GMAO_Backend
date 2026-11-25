using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GMAO.API.Controllers;
using GMAO.API.Security;
using GMAO.Application.DTOs.Auth;
using GMAO.Application.Interfaces;
using GMAO.Application.Security;
using GMAO.Application.Services;
using GMAO.Domain.Constants;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;
using GMAO.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GMAO.Tests;

public class SecurityAndBusinessRemediationTests : IClassFixture<EphemeralPostgresCluster>
{
    private readonly EphemeralPostgresCluster _pgCluster;

    public SecurityAndBusinessRemediationTests(EphemeralPostgresCluster pgCluster)
    {
        _pgCluster = pgCluster;
    }

    private GmaoDbContext CreatePgDb(ITenantContextService tenantService, string? connStr = null)
    {
        var opts = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseNpgsql(connStr ?? _pgCluster.ConnectionString)
            .Options;
        return new GmaoDbContext(opts, tenantService);
    }

    private async Task<string> CreateIsolatedPgDatabaseAsync()
    {
        var dbName = $"gmao_sec_test_{Guid.NewGuid():N}";
        await using (var conn = new Npgsql.NpgsqlConnection(_pgCluster.ConnectionString))
        {
            await conn.OpenAsync();
            await using var createCmd = new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{dbName}\";", conn);
            await createCmd.ExecuteNonQueryAsync();
        }

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(_pgCluster.ConnectionString)
        {
            Database = dbName,
            IncludeErrorDetail = true
        };
        var connStr = builder.ConnectionString;

        await using var tempDb = CreatePgDb(CreateTenantMock(1, isSuperAdmin: true).Object, connStr);
        var script = tempDb.Database.GenerateCreateScript();
        int firstInsertIdx = script.IndexOf("INSERT INTO \"", StringComparison.Ordinal);
        string ddlPart = firstInsertIdx >= 0 ? script[..firstInsertIdx] : script;
        string seedPart = firstInsertIdx >= 0 ? script[firstInsertIdx..] : string.Empty;

        await using (var conn = new Npgsql.NpgsqlConnection(connStr))
        {
            await conn.OpenAsync();
            await using (var ddlCmd = new Npgsql.NpgsqlCommand(ddlPart, conn))
                await ddlCmd.ExecuteNonQueryAsync();

            await using (var socCmd = new Npgsql.NpgsqlCommand(@"
INSERT INTO ""Societes"" (""Id"", ""CodeTenant"", ""Nom"", ""SubscriptionPlan"", ""SubscriptionStatus"", ""SubscriptionAmount"", ""MaxSites"", ""MaxUsers"", ""MaxEquipements"", ""StorageUsedGb"", ""MaxStorageGb"", ""CapacityTonsPerDay"", ""IsActive"", ""CreatedAt"")
VALUES (1, 'seed-soc', 'Seed Societe', 'Enterprise', 'Actif', 850, 3, 25, 150, 0, 50, 450, TRUE, NOW())
ON CONFLICT (""Id"") DO NOTHING;", conn))
                await socCmd.ExecuteNonQueryAsync();

            if (!string.IsNullOrWhiteSpace(seedPart))
            {
                await using var seedCmd = new Npgsql.NpgsqlCommand(seedPart, conn);
                await seedCmd.ExecuteNonQueryAsync();
            }

            await using (var seqCmd = new Npgsql.NpgsqlCommand(@"
SELECT setval(pg_get_serial_sequence('""Societes""', 'Id'), COALESCE((SELECT MAX(""Id"") FROM ""Societes""), 1));
SELECT setval(pg_get_serial_sequence('""Localisations""', 'Id'), COALESCE((SELECT MAX(""Id"") FROM ""Localisations""), 1));
SELECT setval(pg_get_serial_sequence('""Roles""', 'Id'), COALESCE((SELECT MAX(""Id"") FROM ""Roles""), 1));", conn))
                await seqCmd.ExecuteNonQueryAsync();
        }

        return connStr;
    }

    private static Mock<ITenantContextService> CreateTenantMock(int societeId, bool isSuperAdmin = false, bool isImpersonating = false)
    {
        var mock = new Mock<ITenantContextService>();
        mock.Setup(t => t.CurrentSocieteId).Returns(societeId);
        mock.Setup(t => t.GetRequiredTenantId()).Returns(societeId);
        mock.Setup(t => t.IsSuperAdmin).Returns(isSuperAdmin);
        mock.Setup(t => t.IsImpersonating).Returns(isImpersonating);
        return mock;
    }

    private static ClaimsPrincipal CreatePrincipal(int userId, string roleName, int societeId, params string[] explicitPermissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, roleName),
            new("SocieteId", societeId.ToString())
        };
        if (explicitPermissions.Length > 0)
        {
            claims.Add(new Claim(GmaoPermissions.PermissionsLoadedClaim, "true"));
            foreach (var perm in explicitPermissions)
                claims.Add(new Claim("Permission", perm));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static GmaoDbContext CreateInMemoryDb(string dbName, ITenantContextService tenantService)
    {
        var options = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new GmaoDbContext(options, tenantService);
    }

    private static ActionExecutingContext CreateFilterContext(ClaimsPrincipal user)
    {
        var http = new DefaultHttpContext { User = user };
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());
    }

    // ── 1. PlanPreventif Multi-Tenant Isolation (2A) ─────────────────────────
    [Fact]
    public async Task PlansPreventif_CrossTenant_GetUpdateDeleteGenererReplanifier_AreBlocked()
    {
        var dbName = $"PlanTenantTest_{Guid.NewGuid()}";
        var superAdminTenant = new Mock<ITenantContextService>();
        superAdminTenant.Setup(t => t.IsSuperAdmin).Returns(true);
        superAdminTenant.Setup(t => t.IsImpersonating).Returns(false);

        using (var seedDb = CreateInMemoryDb(dbName, superAdminTenant.Object))
        {
            seedDb.Familles.Add(new Famille { Id = 1, Nom = "Convoyeur" });
            seedDb.Equipements.AddRange(
                new Equipement { Id = 10, SocieteId = 1, Code = "EQ-T1", Designation = "Eq Tenant 1", FamilleId = 1 },
                new Equipement { Id = 20, SocieteId = 2, Code = "EQ-T2", Designation = "Eq Tenant 2", FamilleId = 1 }
            );
            seedDb.Users.Add(new User { Id = 100, Nom = "Resp", Prenom = "T1", Email = "r@t1.tn", RoleId = 2, SocieteId = 1, IsActive = true, PasswordHash = "$2a$11$dummy" });
            seedDb.Campagnes.Add(new Campagne { Id = 5, SocieteId = 1, Nom = "Campagne T1", DateDebut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)), Etat = EtatCampagne.EnCours });
            seedDb.PlansPreventifs.AddRange(
                new PlanPreventif { Id = 101, EquipementId = 10, Titre = "Plan T1", Frequence = 30, UniteMesure = "jours", Actif = true },
                new PlanPreventif { Id = 202, EquipementId = 20, Titre = "Plan T2", Frequence = 30, UniteMesure = "jours", Actif = true }
            );
            await seedDb.SaveChangesAsync();
        }

        var t1Mock = CreateTenantMock(1);
        using var t1Db = CreateInMemoryDb(dbName, t1Mock.Object);

        var queryFiltered = await t1Db.PlansPreventifs.ToListAsync();
        Assert.Single(queryFiltered);
        Assert.Equal(101, queryFiltered[0].Id);

        var otRepo = new GenericRepository<OrdresTravail>(t1Db);
        var ctrl = new PlansPreventifController(t1Db, otRepo, t1Mock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(100, GmaoRoles.ResponsableMaintenance, 1) }
            }
        };

        Assert.IsType<NotFoundResult>(await ctrl.GetById(202));
        Assert.IsType<NotFoundResult>(await ctrl.Update(202, new CreatePlanRequest { EquipementId = 10, Titre = "Hijack" }));
        Assert.IsType<NotFoundResult>(await ctrl.Delete(202));
        Assert.IsType<NotFoundResult>(await ctrl.Replanifier(202, new ReplanifierRequest { NouvelleDate = DateTime.UtcNow.AddDays(5) }));
        Assert.IsType<NotFoundResult>(await ctrl.GenererOT(202));

        var crossAssign = await ctrl.Update(101, new CreatePlanRequest { EquipementId = 20, Titre = "Move Eq" });
        Assert.IsType<BadRequestObjectResult>(crossAssign);

        var crossCreate = await ctrl.Create(new CreatePlanRequest { EquipementId = 20, Titre = "Alien Plan" });
        Assert.IsType<BadRequestObjectResult>(crossCreate);

        var genOk = Assert.IsType<OkObjectResult>(await ctrl.GenererOT(101));
        Assert.NotNull(genOk.Value);
        var createdOt = await t1Db.OrdresTravail.FirstOrDefaultAsync(o => o.SocieteId == 1);
        Assert.NotNull(createdOt);
        Assert.Equal(5, createdOt!.CampagneId);
    }

    // ── 2. PasswordHasher Plaintext Removal + SHA256 Rehash (4A) ─────────────
    [Fact]
    public async Task PasswordHasher_RejectsPlaintext_AndAuthServiceRehashesLegacySha256()
    {
        Assert.False(PasswordHasher.VerifyPassword("monMotDePasse", "monMotDePasse"));
        Assert.False(PasswordHasher.VerifyPassword("123456", "123456"));

        var bcryptHash = PasswordHasher.HashPassword("Secret2026!");
        Assert.True(PasswordHasher.VerifyPassword("Secret2026!", bcryptHash));
        Assert.False(PasswordHasher.NeedsRehash(bcryptHash));

        var shaBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("admin"));
        var shaHex = Convert.ToHexString(shaBytes).ToLowerInvariant();
        Assert.True(PasswordHasher.VerifyPassword("admin", shaHex));
        Assert.True(PasswordHasher.NeedsRehash(shaHex));

        var user = new User
        {
            Id = 5,
            Email = "legacy@pomodoro.tn",
            Nom = "Pomodoro",
            Prenom = "Admin",
            RoleId = GmaoRoles.AdministrateurId,
            Role = new Role { Id = GmaoRoles.AdministrateurId, Nom = GmaoRoles.Administrateur },
            SocieteId = 1,
            Societe = new Societe { Id = 1, Nom = "Pomodoro", CodeTenant = "pomodoro" },
            EquipeId = 12,
            IsActive = true,
            PasswordHash = shaHex
        };

        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(r => r.GetByEmailAsync("legacy@pomodoro.tn")).ReturnsAsync(user);
        userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        var rtRepoMock = new Mock<IGenericRepository<RefreshToken>>();
        rtRepoMock.Setup(r => r.AddAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "SuperLongJwtSigningKeyForUnitTestsOnly2026!!",
                ["JwtSettings:Issuer"] = "GMAO.API",
                ["JwtSettings:Audience"] = "GMAO.Frontend",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "15",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7"
            })
            .Build();

        var authService = new AuthService(userRepoMock.Object, rtRepoMock.Object, cfg);
        var res = await authService.LoginAsync("legacy@pomodoro.tn", "admin");

        Assert.True(PasswordHasher.IsBcryptHash(user.PasswordHash));
        Assert.Equal(12, res.User.EquipeId);
        Assert.NotEmpty(res.User.Permissions);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(res.AccessToken);
        Assert.Equal("12", jwt.Claims.First(c => c.Type == "EquipeId").Value);
        Assert.Equal("true", jwt.Claims.First(c => c.Type == GmaoPermissions.PermissionsLoadedClaim).Value);
    }

    // ── 3. HasPermissionAttribute & RBAC Unification (2C) ────────────────────
    [Fact]
    public async Task HasPermissionAttribute_AllowsAdministrateur_DeniesTechnicien_RespectsExplicitEmpty()
    {
        bool invoked = false;
        Task<ActionExecutedContext> Next()
        {
            invoked = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        }

        var filter = new HasPermissionAttribute(GmaoPermissions.UserCreate);
        var adminCtx = CreateFilterContext(CreatePrincipal(1, GmaoRoles.Administrateur, 1));
        await filter.OnActionExecutionAsync(adminCtx, Next);
        Assert.True(invoked);
        Assert.Null(adminCtx.Result);

        invoked = false;
        var techCtx = CreateFilterContext(CreatePrincipal(4, GmaoRoles.Technicien, 1));
        await filter.OnActionExecutionAsync(techCtx, Next);
        Assert.False(invoked);
        Assert.IsType<ForbidResult>(techCtx.Result);

        var strippedCtx = CreateFilterContext(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "2"),
            new Claim(ClaimTypes.Role, GmaoRoles.ResponsableMaintenance),
            new Claim(GmaoPermissions.PermissionsLoadedClaim, "true"),
            new Claim("SocieteId", "1")
        }, "TestAuth")));
        var woFilter = new HasPermissionAttribute(GmaoPermissions.WorkOrderView);
        await woFilter.OnActionExecutionAsync(strippedCtx, Next);
        Assert.IsType<ForbidResult>(strippedCtx.Result);
    }

    // ── 4. UsersController Privilege Escalation & Safe Delete (2B) ───────────
    [Fact]
    public async Task UsersController_EnforcesPrivilegeEscalation_AndConflictOnReferencedDelete()
    {
        var dbName = $"UsersSecTest_{Guid.NewGuid()}";
        var tenant1 = CreateTenantMock(1);
        using var db = CreateInMemoryDb(dbName, tenant1.Object);

        db.Roles.AddRange(
            new Role { Id = 1, Nom = GmaoRoles.Administrateur },
            new Role { Id = 2, Nom = GmaoRoles.ResponsableMaintenance },
            new Role { Id = 4, Nom = GmaoRoles.Technicien },
            new Role { Id = 7, Nom = GmaoRoles.SuperAdmin }
        );
        db.Societes.Add(new Societe { Id = 1, Nom = "Pomodoro", CodeTenant = "pomodoro", IsActive = true, MaxUsers = 5 });
        db.Users.AddRange(
            new User { Id = 10, Nom = "Admin", Prenom = "T1", Email = "admin@t1.tn", RoleId = 1, SocieteId = 1, IsActive = true, PasswordHash = "$2a$11$x" },
            new User { Id = 11, Nom = "Tech", Prenom = "Busy", Email = "tech@t1.tn", RoleId = 4, SocieteId = 1, IsActive = true, PasswordHash = "$2a$11$x" }
        );
        db.OrdresTravail.Add(new OrdresTravail { Id = 1, NumeroOT = "OT-1", SocieteId = 1, EquipementId = 1, ResponsableId = 10, TechnicienId = 11 });
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);
        var authMock = new Mock<IAuthService>();
        authMock.Setup(a => a.RegisterAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync((User u, string _) => { u.Id = 99; return u; });

        var nonAdminManager = new UsersController(authMock.Object, repo, tenant1.Object, db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(20, GmaoRoles.ResponsableMaintenance, 1, GmaoPermissions.UserCreate, GmaoPermissions.UserUpdate)
                }
            }
        };

        var createAdminAttempt = await nonAdminManager.CreateUser(new RegisterDto
        {
            User = new User { Nom = "Rogue", Email = "rogue@t1.tn", RoleId = GmaoRoles.AdministrateurId },
            Password = "Password123!"
        });
        Assert.IsType<ForbidResult>(createAdminAttempt);

        var editAdminAttempt = await nonAdminManager.UpdateUser(10, new UpdateUserDto { IsActive = false });
        Assert.IsType<ForbidResult>(editAdminAttempt);

        var adminCtrl = new UsersController(authMock.Object, repo, tenant1.Object, db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(10, GmaoRoles.Administrateur, 1) }
            }
        };

        var selfDeactivate = await adminCtrl.UpdateUser(10, new UpdateUserDto { IsActive = false });
        Assert.IsType<BadRequestObjectResult>(selfDeactivate);

        var deleteReferenced = await adminCtrl.DeleteUser(11);
        Assert.IsType<ConflictObjectResult>(deleteReferenced);
    }

    // ── 5. Real PostgreSQL 18 Concurrency: Quota MaxUsers (2B) ───────────────
    [Fact]
    public async Task UsersController_ConcurrentCreates_OnRealPostgres_NeverExceedMaxUsers()
    {
        var connStr = await CreateIsolatedPgDatabaseAsync();
        var seedTenant = CreateTenantMock(100, isSuperAdmin: true);
        using (var seedDb = CreatePgDb(seedTenant.Object, connStr))
        {
            seedDb.Societes.Add(new Societe
            {
                Id = 100,
                Nom = "Usine Quota",
                CodeTenant = "quota-t100",
                IsActive = true,
                MaxUsers = 2
            });
            seedDb.Users.Add(new User
            {
                Id = 1000,
                Nom = "Admin",
                Prenom = "Root",
                Email = "admin@quota100.tn",
                PasswordHash = PasswordHasher.HashPassword("Admin123!"),
                RoleId = GmaoRoles.AdministrateurId,
                SocieteId = 100,
                IsActive = true
            });
            await seedDb.SaveChangesAsync();
            await seedDb.Database.ExecuteSqlRawAsync(@"SELECT setval('""Users_Id_seq""', 1500, true); SELECT setval('""Societes_Id_seq""', 250, true);");
        }

        var cfg = new ConfigurationBuilder().Build();
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<IActionResult> TryCreateUserAsync(string email)
        {
            var tenantMock = CreateTenantMock(100);
            await using var db = CreatePgDb(tenantMock.Object, connStr);
            var userRepo = new UserRepository(db);
            var rtRepo = new GenericRepository<RefreshToken>(db);
            var auth = new AuthService(userRepo, rtRepo, cfg);
            var ctrl = new UsersController(auth, userRepo, tenantMock.Object, db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = CreatePrincipal(1000, GmaoRoles.Administrateur, 100) }
                }
            };

            await barrier.Task;
            return await ctrl.CreateUser(new RegisterDto
            {
                User = new User { Nom = "Concurrent", Prenom = email, Email = email, RoleId = GmaoRoles.TechnicienId, IsActive = true },
                Password = "Password123!"
            });
        }

        var t1 = Task.Run(() => TryCreateUserAsync("u1@quota100.tn"));
        var t2 = Task.Run(() => TryCreateUserAsync("u2@quota100.tn"));
        barrier.SetResult();
        var results = await Task.WhenAll(t1, t2);

        Assert.Equal(1, results.Count(r => r is OkObjectResult));
        Assert.Equal(1, results.Count(r => r is BadRequestObjectResult));

        using var verifyDb = CreatePgDb(CreateTenantMock(100).Object, connStr);
        var activeCount = await verifyDb.Users.CountAsync(u => u.SocieteId == 100 && u.IsActive);
        Assert.Equal(2, activeCount);
    }

    // ── 6. MouvementsStock Security, 5 Types & PG Concurrency (2E) ───────────
    [Fact]
    public async Task MouvementsStock_EnforcesTenantValidation_All5Types_AndConcurrentSortieOnPostgres()
    {
        var connStr = await CreateIsolatedPgDatabaseAsync();
        var saMock = CreateTenantMock(200, isSuperAdmin: true);
        using (var seedDb = CreatePgDb(saMock.Object, connStr))
        {
            seedDb.Societes.AddRange(
                new Societe { Id = 200, Nom = "T200", CodeTenant = "t200", IsActive = true },
                new Societe { Id = 201, Nom = "T201", CodeTenant = "t201", IsActive = true }
            );
            seedDb.Users.Add(new User { Id = 2000, Nom = "Chef", Prenom = "T200", Email = "c@t200.tn", RoleId = 3, SocieteId = 200, PasswordHash = "$2a$11$x", IsActive = true });
            seedDb.Pieces.AddRange(
                new Piece { Id = 2001, SocieteId = 200, Reference = "P-T200", Designation = "Joint T200", StockActuel = 10, StockMinimum = 2, StockMaximum = 50, PrixUnitaire = 15m },
                new Piece { Id = 2002, SocieteId = 201, Reference = "P-T201", Designation = "Joint T201", StockActuel = 20, StockMinimum = 2, StockMaximum = 50, PrixUnitaire = 20m }
            );
            await seedDb.SaveChangesAsync();
            await seedDb.Database.ExecuteSqlRawAsync(@"SELECT setval('""MouvementsStock_Id_seq""', 100, true); SELECT setval('""Users_Id_seq""', 2500, true); SELECT setval('""Pieces_Id_seq""', 2500, true);");
        }

        var t1Mock = CreateTenantMock(200);
        using (var db1 = CreatePgDb(t1Mock.Object, connStr))
        {
            var ctrl = new MouvementsStockController(new GenericRepository<MouvementStock>(db1), t1Mock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = CreatePrincipal(2000, GmaoRoles.ChefEquipe, 200) }
                }
            };

            Assert.IsType<NotFoundObjectResult>(await ctrl.Create(new MouvementStock { PieceId = 2002, Type = TypeMouvementStock.Sortie, Quantite = 5 }, db1));
            Assert.IsType<BadRequestObjectResult>(await ctrl.Create(new MouvementStock { PieceId = 2001, Type = TypeMouvementStock.Entree, Quantite = 0 }, db1));
            Assert.IsType<BadRequestObjectResult>(await ctrl.Create(new MouvementStock { PieceId = 2001, Type = TypeMouvementStock.Sortie, Quantite = 99 }, db1));

            Assert.IsType<CreatedAtActionResult>(await ctrl.Create(new MouvementStock { PieceId = 2001, Type = TypeMouvementStock.Entree, Quantite = 5 }, db1));
            Assert.IsType<CreatedAtActionResult>(await ctrl.Create(new MouvementStock { PieceId = 2001, Type = TypeMouvementStock.Retour, Quantite = 2 }, db1));
            Assert.IsType<CreatedAtActionResult>(await ctrl.Create(new MouvementStock { PieceId = 2001, Type = TypeMouvementStock.Casse, Quantite = 3 }, db1));
            Assert.IsType<CreatedAtActionResult>(await ctrl.Create(new MouvementStock { PieceId = 2001, Type = TypeMouvementStock.Inventaire, Quantite = 10 }, db1));

            var pieceState = await db1.Pieces.AsNoTracking().FirstAsync(p => p.Id == 2001);
            Assert.Equal(10m, pieceState.StockActuel);
        }

        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<IActionResult> TrySortieAsync()
        {
            var m = CreateTenantMock(200);
            await using var db = CreatePgDb(m.Object, connStr);
            var c = new MouvementsStockController(new GenericRepository<MouvementStock>(db), m.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = CreatePrincipal(2000, GmaoRoles.ChefEquipe, 200) }
                }
            };
            await barrier.Task;
            return await c.Create(new MouvementStock { PieceId = 2001, Type = TypeMouvementStock.Sortie, Quantite = 6 }, db);
        }

        var c1 = Task.Run(TrySortieAsync);
        var c2 = Task.Run(TrySortieAsync);
        barrier.SetResult();
        var concRes = await Task.WhenAll(c1, c2);

        Assert.Equal(1, concRes.Count(r => r is CreatedAtActionResult));
        Assert.Equal(1, concRes.Count(r => r is BadRequestObjectResult));

        using var verifyDb = CreatePgDb(t1Mock.Object, connStr);
        var finalPiece = await verifyDb.Pieces.AsNoTracking().FirstAsync(p => p.Id == 2001);
        Assert.Equal(4m, finalPiece.StockActuel);
    }

    // ── 7. OrdresTravail Transitions + Intervention + Repository/Service ────
    [Fact]
    public async Task OrdresTravail_InvalidJumpAndTerminalMutate_ReturnConflict_AndInterventionPersists()
    {
        var dbName = $"WoExtTest_{Guid.NewGuid()}";
        var t1Mock = CreateTenantMock(1);
        using var db = CreateInMemoryDb(dbName, t1Mock.Object);

        db.Familles.Add(new Famille { Id = 1, Nom = "Cuves" });
        db.Users.Add(new User { Id = 5, Nom = "Resp", Prenom = "T1", Email = "r@t1.tn", RoleId = 2, SocieteId = 1, PasswordHash = "$2a$11$x", IsActive = true });
        db.Equipements.Add(new Equipement { Id = 1, SocieteId = 1, Code = "EQ-01", Designation = "Cuve", FamilleId = 1 });
        db.OrdresTravail.Add(new OrdresTravail
        {
            Id = 50,
            NumeroOT = "OT-2026-S1-050",
            SocieteId = 1,
            EquipementId = 1,
            ResponsableId = 5,
            TechnicienId = 8,
            Statut = StatutOT.Planifie
        });
        await db.SaveChangesAsync();

        var repo = new OrdreTravailRepository(db);
        var incRepo = new GenericRepository<DemandeIntervention>(db);
        var ctrl = new OrdresTravailController(repo, incRepo, db, t1Mock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(5, GmaoRoles.ResponsableMaintenance, 1)
                }
            }
        };

        Assert.IsType<ConflictObjectResult>(await ctrl.Update(50, new OrdresTravail { Id = 50, Statut = StatutOT.Cloture }));

        Assert.IsType<NoContentResult>(await ctrl.Update(50, new OrdresTravail { Id = 50, Statut = StatutOT.EnCours }));
        Assert.IsType<NoContentResult>(await ctrl.Update(50, new OrdresTravail
        {
            Id = 50,
            Statut = StatutOT.Termine,
            CoutSousTraitance = 320m,
            Intervention = new Intervention
            {
                Diagnostic = "Roulement grippé",
                Solution = "Remplacement palier",
                TempsPasse = 2.5m
            }
        }));

        var otLoaded = await repo.GetByNumeroAsync("OT-2026-S1-050");
        Assert.NotNull(otLoaded);
        Assert.Equal(320m, otLoaded!.CoutSousTraitance);
        Assert.Equal("Roulement grippé", otLoaded.Intervention?.Diagnostic);
        Assert.Equal("Remplacement palier", otLoaded.Intervention?.Solution);
        Assert.Equal(2.5m, otLoaded.Intervention?.TempsPasse);

        var eqRepo = new EquipementRepository(db);
        var intRepo = new GenericRepository<Intervention>(db);
        var maintSvc = new MaintenanceService(eqRepo, repo, incRepo, intRepo);
        var dem = await maintSvc.DeclareBreakdownAsync(1, 5, "Fuite vapeur");
        Assert.Equal(1, dem.SocieteId);
        Assert.Equal(EtatEquipement.EnPanne, (await eqRepo.GetByIdAsync(1))!.Etat);
    }

    // ── 8. Localisations Default Absence in EquipementController ────────────
    [Fact]
    public async Task Equipement_CreateWithoutLocalisation_NeverHardcodesId1AcrossTenants()
    {
        var dbName = $"EqLocTest_{Guid.NewGuid()}";
        var saMock = CreateTenantMock(1, isSuperAdmin: true);
        using (var seedDb = CreateInMemoryDb(dbName, saMock.Object))
        {
            seedDb.Localisations.Add(new Localisation { Id = 1, SocieteId = 1, Nom = "Usine T1" });
            seedDb.Localisations.Add(new Localisation { Id = 20, SocieteId = 2, Nom = "Usine T2" });
            seedDb.Familles.Add(new Famille { Id = 1, Nom = "Pompe" });
            await seedDb.SaveChangesAsync();
        }

        var t2Mock = CreateTenantMock(2);
        using var db = CreateInMemoryDb(dbName, t2Mock.Object);

        var ctrl = new EquipementController(
            new EquipementRepository(db),
            new GenericRepository<Piece>(db),
            db,
            t2Mock.Object);

        var res = await ctrl.Create(new GMAO.Application.DTOs.Equipements.EquipementInputDto
        {
            Designation = "Pompe T2",
            FamilleId = 1,
            LocalisationId = 1
        });
        var created = Assert.IsType<CreatedAtActionResult>(res);
        var dto = Assert.IsType<GMAO.Application.DTOs.Equipements.EquipementDto>(created.Value);
        Assert.NotEqual(1, dto.LocalisationId);
    }
}
