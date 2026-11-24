using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Infrastructure.Audit;
using GMAO.Infrastructure.Cdc;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace GMAO.Tests;

public sealed class EphemeralPostgresCluster : IAsyncLifetime
{
    private const string PgBinDir = @"C:\Program Files\PostgreSQL\18\bin";
    private readonly string _walLevel;
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"gmao_pg18_test_{Guid.NewGuid():N}");

    public int Port { get; private set; }
    public string DatabaseName { get; } = "gmao_cdc_test";
    public string ConnectionString => $"Host=127.0.0.1;Port={Port};Database={DatabaseName};Username=postgres;Pooling=true;";
    public string ReplicationConnectionString => $"Host=127.0.0.1;Port={Port};Database={DatabaseName};Username=postgres;Pooling=false;Timeout=15;";

    public EphemeralPostgresCluster() : this("logical")
    {
    }

    private EphemeralPostgresCluster(string walLevel)
    {
        _walLevel = walLevel;
    }

    public static EphemeralPostgresCluster CreateReplicaCluster() => new("replica");

    public async Task InitializeAsync()
    {
        var initdb = Path.Combine(PgBinDir, "initdb.exe");
        var pgCtl = Path.Combine(PgBinDir, "pg_ctl.exe");

        if (!File.Exists(initdb) || !File.Exists(pgCtl))
            throw new FileNotFoundException($"PostgreSQL 18 binaries introuvables sous {PgBinDir}");

        Port = ReserveFreeTcpPort();
        Directory.CreateDirectory(_dataDir);

        await RunProcessAsync(initdb, $"-D \"{_dataDir}\" -U postgres -A trust --encoding=UTF8 --locale=C", captureOutput: true);

        var confPath = Path.Combine(_dataDir, "postgresql.conf");
        await File.AppendAllTextAsync(confPath, Environment.NewLine + string.Join(Environment.NewLine, new[]
        {
            "listen_addresses = '127.0.0.1'",
            $"port = {Port}",
            $"wal_level = {_walLevel}",
            "max_replication_slots = 4",
            "max_wal_senders = 4",
            "max_worker_processes = 4",
            "logging_collector = off",
            "fsync = off"
        }) + Environment.NewLine);

        var logPath = Path.Combine(_dataDir, "pg_startup.log");
        await RunProcessAsync(pgCtl, $"start -D \"{_dataDir}\" -w -l \"{logPath}\" -o \"-p {Port}\"", captureOutput: false, errorLogPath: logPath);

        await using var conn = new NpgsqlConnection($"Host=127.0.0.1;Port={Port};Database=postgres;Username=postgres;Pooling=false;");
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{DatabaseName}\";", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        try { NpgsqlConnection.ClearAllPools(); } catch { }

        var pgCtl = Path.Combine(PgBinDir, "pg_ctl.exe");
        if (File.Exists(pgCtl) && Directory.Exists(_dataDir))
        {
            try
            {
                await RunProcessAsync(pgCtl, $"stop -D \"{_dataDir}\" -m immediate -w", captureOutput: false, ignoreErrors: true);
            }
            catch { }
        }

        if (Directory.Exists(_dataDir))
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Directory.Delete(_dataDir, recursive: true);
                    break;
                }
                catch
                {
                    await Task.Delay(200);
                }
            }
        }
    }

    private static int ReserveFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task RunProcessAsync(string exePath, string arguments, bool captureOutput, string? errorLogPath = null, bool ignoreErrors = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Impossible de lancer {exePath}");
        string stdOut = captureOutput ? await proc.StandardOutput.ReadToEndAsync() : "";
        string stdErr = captureOutput ? await proc.StandardError.ReadToEndAsync() : "";

        await proc.WaitForExitAsync();

        if (!ignoreErrors && proc.ExitCode != 0)
        {
            string logText = !string.IsNullOrEmpty(errorLogPath) && File.Exists(errorLogPath)
                ? await File.ReadAllTextAsync(errorLogPath)
                : "";
            throw new InvalidOperationException(
                $"{Path.GetFileName(exePath)} a échoué (code {proc.ExitCode}): {stdErr}\n{stdOut}\n{logText}");
        }
    }
}

public class RealPostgresCdcAndAuditTests : IClassFixture<EphemeralPostgresCluster>
{
    private readonly EphemeralPostgresCluster _pgCluster;

    public RealPostgresCdcAndAuditTests(EphemeralPostgresCluster pgCluster)
    {
        _pgCluster = pgCluster;
    }

    private sealed class SuperAdminTestTenantContext : ITenantContextService
    {
        public int? CurrentSocieteId => null;
        public bool IsSuperAdmin => true;
        public bool IsImpersonating => false;
        public int GetRequiredTenantId() => 1;
    }

    private static IHttpContextAccessor CreateHttpContextAccessor(int userId, string name, string role, int societeId, string correlationId)
    {
        var ctx = new DefaultHttpContext
        {
            TraceIdentifier = correlationId,
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.Role, role),
                new Claim("SocieteId", societeId.ToString())
            }, "TestAuth"))
        };
        ctx.Connection.RemoteIpAddress = IPAddress.Loopback;

        return new HttpContextAccessor { HttpContext = ctx };
    }

    private ServiceProvider BuildServiceProvider(
        string appConnStr,
        PostgresCdcOptions cdcOptions,
        IHttpContextAccessor? httpAccessor = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContextService, SuperAdminTestTenantContext>();
        if (httpAccessor != null)
            services.AddSingleton(httpAccessor);
        else
            services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());

        services.AddSingleton(sp => new AuditSaveChangesInterceptor(
            sp.GetService<IHttpContextAccessor>(),
            NullLogger<AuditSaveChangesInterceptor>.Instance));

        services.AddDbContext<GmaoDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(appConnStr)
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<IBusinessCompensationService, BusinessCompensationService>();
        services.AddScoped<IRedoLogService, RedoLogService>();
        services.AddSingleton(Options.Create(cdcOptions));
        services.AddSingleton(sp => new PostgresCdcIngestionService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IOptions<PostgresCdcOptions>>(),
            NullLogger<PostgresCdcIngestionService>.Instance));

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ProbeCapabilities_DetectsDisabled_NotConfigured_NetworkBlocked_IncompatibleWalLevel_And_ReadyLogical()
    {
        // Créer le schéma complet (incluant AuditTrailEntries, CdcEvents, CdcWorkerStates, Documents étendus) sur PostgreSQL 18 isolé
        await using (var spInit = BuildServiceProvider(_pgCluster.ConnectionString, new PostgresCdcOptions { Enabled = false }))
        {
            using var scope = spInit.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GmaoDbContext>();
            await InitializeRealPostgresSchemaAsync(db, _pgCluster.ConnectionString);
        }

        // 1. Disabled
        await using (var sp = BuildServiceProvider(_pgCluster.ConnectionString, new PostgresCdcOptions
        {
            Enabled = false,
            SlotName = "probe_slot_disabled"
        }))
        {
            var svc = sp.GetRequiredService<PostgresCdcIngestionService>();
            var st = await svc.ProbeCapabilitiesAsync();
            Assert.Equal("Disabled", st.Status);
        }

        // 2. NotConfigured (placeholder / vide)
        await using (var sp = BuildServiceProvider(_pgCluster.ConnectionString, new PostgresCdcOptions
        {
            Enabled = true,
            ReplicationConnectionString = "REPLACED_BY_ENV_VAR",
            SlotName = "probe_slot_notcfg"
        }))
        {
            var svc = sp.GetRequiredService<PostgresCdcIngestionService>();
            var st = await svc.ProbeCapabilitiesAsync();
            Assert.Equal("NotConfigured", st.Status);
        }

        // 3. NetworkBlocked (port fermé injoignable)
        await using (var sp = BuildServiceProvider(_pgCluster.ConnectionString, new PostgresCdcOptions
        {
            Enabled = true,
            ReplicationConnectionString = "Host=127.0.0.1;Port=59998;Database=gmao_cdc_test;Username=postgres;Timeout=2;",
            SlotName = "probe_slot_net"
        }))
        {
            var svc = sp.GetRequiredService<PostgresCdcIngestionService>();
            var st = await svc.ProbeCapabilitiesAsync();
            Assert.Equal("NetworkBlocked", st.Status);
        }

        // 4. Ready sur vrai PostgreSQL 18 wal_level=logical
        await using (var sp = BuildServiceProvider(_pgCluster.ConnectionString, new PostgresCdcOptions
        {
            Enabled = true,
            ReplicationConnectionString = _pgCluster.ReplicationConnectionString,
            SlotName = "probe_slot_ready"
        }))
        {
            var svc = sp.GetRequiredService<PostgresCdcIngestionService>();
            var st = await svc.ProbeCapabilitiesAsync();
            Assert.Equal("Ready", st.Status);
            Assert.Equal("logical", st.WalLevel);
            Assert.Contains("PostgreSQL 18", st.PostgresVersion);
            Assert.False(string.IsNullOrWhiteSpace(st.CurrentServerWalLsn));
            Assert.True(st.MaxReplicationSlots >= 4);
        }

        // 5. IncompatibleWalLevel sur un second cluster wal_level=replica réel
        var replicaCluster = EphemeralPostgresCluster.CreateReplicaCluster();
        await replicaCluster.InitializeAsync();
        try
        {
            await using var spRep = BuildServiceProvider(_pgCluster.ConnectionString, new PostgresCdcOptions
            {
                Enabled = true,
                ReplicationConnectionString = replicaCluster.ReplicationConnectionString,
                SlotName = "probe_slot_replica_check"
            });
            var svcRep = spRep.GetRequiredService<PostgresCdcIngestionService>();
            var stRep = await svcRep.ProbeCapabilitiesAsync();
            Assert.Equal("IncompatibleWalLevel", stRep.Status);
            Assert.Equal("replica", stRep.WalLevel);
        }
        finally
        {
            await replicaCluster.DisposeAsync();
        }
    }

    [Fact]
    public async Task EndToEnd_RealPg18_PgOutputCdc_AuditTrailCorrelation_Masking_Rollback_Resume_And_Compensation()
    {
        var slotName = "gmao_cdc_e2e_slot";
        var pubName = "gmao_cdc_e2e_pub";
        var httpAccessor = CreateHttpContextAccessor(101, "Super Admin Test", "SuperAdmin", 1, "corr-pg18-e2e");

        var cdcOpts = new PostgresCdcOptions
        {
            Enabled = true,
            ReplicationConnectionString = _pgCluster.ReplicationConnectionString,
            SlotName = slotName,
            PublicationName = pubName,
            ReconnectDelaySeconds = 1
        };

        await using var sp = BuildServiceProvider(_pgCluster.ConnectionString, cdcOpts, httpAccessor);

        // Schéma complet + Seed Société 1 pour FK
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GmaoDbContext>();
            await InitializeRealPostgresSchemaAsync(db, _pgCluster.ConnectionString);

            if (!await db.Societes.IgnoreQueryFilters().AnyAsync(s => s.Id == 1))
            {
                db.Societes.Add(new Societe
                {
                    Nom = "Usine Concentré Cap Bon",
                    CodeTenant = "capbon-cdc",
                    Adresse = "Nabeul",
                    SubscriptionStatus = "Actif",
                    SubscriptionPlan = "Enterprise"
                });
                await db.SaveChangesAsync();
            }
        }

        var cdcService = sp.GetRequiredService<PostgresCdcIngestionService>();

        // 1. Démarrer le worker de décodage logique pgoutput en arrière-plan
        using var cts1 = new CancellationTokenSource();
        var workerTask1 = Task.Run(() => cdcService.RunStreamingSessionAsync(cts1.Token));

        await WaitUntilAsync(async () =>
        {
            using var s = sp.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var w = await db.CdcWorkerStates.FirstOrDefaultAsync(x => x.SlotName == slotName);
            return w?.Status == "ActiveStreaming";
        }, TimeSpan.FromSeconds(15), "Le worker pgoutput n'a pas atteint ActiveStreaming.");

        // 2. INSERT Equipement -> Vérifier CDC Event + Audit Trail + XID identique + Corrélation
        int eqId;
        string? insertXid;
        using (var s = sp.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var eq = new Equipement
            {
                SocieteId = 1,
                FamilleId = 1,
                Code = "EVAP-CDC-01",
                Designation = "Evaporateur Sous Vide Inicial",
                Criticite = CriticiteEquipement.Haute,
                Etat = EtatEquipement.EnService
            };
            db.Equipements.Add(eq);
            await db.SaveChangesAsync();
            eqId = eq.Id;

            var auditIns = await db.AuditTrailEntries
                .OrderByDescending(a => a.Id)
                .FirstAsync(a => a.EntityName == "Equipements" && a.EntityId == eqId.ToString());

            insertXid = auditIns.TransactionId;
            Assert.False(string.IsNullOrWhiteSpace(insertXid));
            Assert.Equal("corr-pg18-e2e", auditIns.CorrelationId);
            Assert.Equal("CREATE", auditIns.Action);
        }

        await WaitUntilAsync(async () =>
        {
            using var s = sp.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            return await db.CdcEvents.AnyAsync(c =>
                c.TableName == "Equipements" &&
                c.Operation == "INSERT" &&
                c.TransactionId == insertXid);
        }, TimeSpan.FromSeconds(15), "CdcEvent INSERT non reçu via pgoutput.");

        // 3. UPDATE Equipement + Corrélation RedoLogService + Compensation contrôlée
        long updateAuditId;
        string? updateXid;
        using (var s = sp.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var eq = await db.Equipements.FirstAsync(e => e.Id == eqId);
            eq.Designation = "Evaporateur Modifié Par Erreur";
            eq.Criticite = CriticiteEquipement.Faible;
            await db.SaveChangesAsync();

            var auditUpd = await db.AuditTrailEntries
                .OrderByDescending(a => a.Id)
                .FirstAsync(a => a.EntityName == "Equipements" && a.EntityId == eqId.ToString() && a.Action == "UPDATE");
            updateAuditId = auditUpd.Id;
            updateXid = auditUpd.TransactionId;
        }

        await WaitUntilAsync(async () =>
        {
            using var s = sp.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            return await db.CdcEvents.AnyAsync(c =>
                c.TableName == "Equipements" &&
                c.Operation == "UPDATE" &&
                c.TransactionId == updateXid);
        }, TimeSpan.FromSeconds(15), "CdcEvent UPDATE non reçu via pgoutput.");

        using (var s = sp.CreateScope())
        {
            var redoSvc = s.ServiceProvider.GetRequiredService<IRedoLogService>();
            var logs = await redoSvc.QueryLogsAsync(new RedoLogQueryParams
            {
                Source = "AUDIT_TRAIL",
                TableName = "Equipements",
                TransactionId = updateXid
            });
            Assert.Contains(logs.Items, item => item.HasWalCorrelation && item.TransactionId == updateXid && !string.IsNullOrWhiteSpace(item.Lsn));

            var compSvc = s.ServiceProvider.GetRequiredService<IBusinessCompensationService>();
            var compRes = await compSvc.CompensateAsync(updateAuditId, 101, "Super Admin Test");
            Assert.True(compRes.Success);
            Assert.Equal(200, compRes.HttpStatusCode);

            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var restoredEq = await db.Equipements.AsNoTracking().FirstAsync(e => e.Id == eqId);
            Assert.Equal("Evaporateur Sous Vide Inicial", restoredEq.Designation);
            Assert.Equal(CriticiteEquipement.Haute, restoredEq.Criticite);
        }

        // 4. Test Optimistic Drift Check -> 409 Conflict
        long driftAuditId;
        using (var s = sp.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var eq = await db.Equipements.FirstAsync(e => e.Id == eqId);
            eq.Designation = "Etat Intermediare";
            await db.SaveChangesAsync();

            driftAuditId = await db.AuditTrailEntries
                .Where(a => a.EntityName == "Equipements" && a.EntityId == eqId.ToString() && a.Action == "UPDATE")
                .MaxAsync(a => a.Id);

            // Modification concurrente postérieure créant une dérive
            eq.Designation = "Modification Concurrente Plus Recente";
            await db.SaveChangesAsync();

            var compSvc = s.ServiceProvider.GetRequiredService<IBusinessCompensationService>();
            var conflictRes = await compSvc.CompensateAsync(driftAuditId, 101, "Super Admin Test");
            Assert.False(conflictRes.Success);
            Assert.Equal(409, conflictRes.HttpStatusCode);
        }

        // 5. Masquage des champs sensibles (PasswordHash) dans AuditTrail ET CdcEvents
        string secretHash = "$2a$11$UltraSecretBcryptHashDoNotExpose999";
        using (var s = sp.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var u = new User
            {
                SocieteId = 1,
                RoleId = 4,
                Nom = "Test",
                Prenom = "Technicien",
                Email = $"mask_{Guid.NewGuid():N}@capbon.tn",
                PasswordHash = secretHash,
                IsActive = true
            };
            db.Users.Add(u);
            await db.SaveChangesAsync();
        }

        await WaitUntilAsync(async () =>
        {
            using var s = sp.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            return await db.CdcEvents.AnyAsync(c => c.TableName == "Users" && c.Operation == "INSERT");
        }, TimeSpan.FromSeconds(15), "CdcEvent Users INSERT non reçu.");

        using (var s = sp.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var userAudit = await db.AuditTrailEntries.OrderByDescending(a => a.Id).FirstAsync(a => a.EntityName == "Users");
            var userCdc = await db.CdcEvents.OrderByDescending(c => c.Id).FirstAsync(c => c.TableName == "Users");

            Assert.DoesNotContain(secretHash, userAudit.NewValuesJson ?? "");
            Assert.Contains("[REDACTED]", userAudit.NewValuesJson ?? "");
            Assert.DoesNotContain(secretHash, userCdc.NewValuesJson ?? "");
            Assert.Contains("[REDACTED]", userCdc.NewValuesJson ?? "");
        }

        // 6. Transaction avortée / rollback -> ZERO AuditTrailEntry & ZERO CdcEvent persistés
        string abortedCode = $"ABORT-{Guid.NewGuid():N}"[..16];
        using (var s = sp.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            db.Equipements.Add(new Equipement
            {
                SocieteId = 1,
                FamilleId = 999999, // FK inexistante -> échec PostgreSQL !
                Code = abortedCode,
                Designation = "Ne Doit Pas Exister"
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

            bool auditCreatedForAborted = await db.AuditTrailEntries
                .AnyAsync(a => a.NewValuesJson != null && a.NewValuesJson.Contains(abortedCode));
            Assert.False(auditCreatedForAborted);
        }

        // 7. DELETE via pgoutput + Déconnexion / Modification hors-ligne / Reprise LSN sans perte ni doublon
        using (var s = sp.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var eqToDel = await db.Equipements.FirstAsync(e => e.Id == eqId);
            db.Equipements.Remove(eqToDel);
            await db.SaveChangesAsync();
        }

        await WaitUntilAsync(async () =>
        {
            using var s = sp.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            return await db.CdcEvents.AnyAsync(c =>
                c.TableName == "Equipements" &&
                c.Operation == "DELETE" &&
                c.RecordId == eqId.ToString());
        }, TimeSpan.FromSeconds(15), "CdcEvent DELETE non reçu via pgoutput.");

        // Vérifier que DELETE est non-compensable (HTTP 422)
        using (var s = sp.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var delAudit = await db.AuditTrailEntries
                .OrderByDescending(a => a.Id)
                .FirstAsync(a => a.EntityName == "Equipements" && a.Action == "DELETE");
            var compSvc = s.ServiceProvider.GetRequiredService<IBusinessCompensationService>();
            var delComp = await compSvc.CompensateAsync(delAudit.Id, 101, "Super Admin Test");
            Assert.False(delComp.Success);
            Assert.Equal(422, delComp.HttpStatusCode);
        }

        // Déconnecter proprement le worker CDC
        string? lsnBeforeDisconnect;
        cts1.Cancel();
        try { await workerTask1; } catch (OperationCanceledException) { }

        using (var s = sp.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var st = await db.CdcWorkerStates.FirstAsync(x => x.SlotName == slotName);
            lsnBeforeDisconnect = st.LastConsumedLsn;
            Assert.False(string.IsNullOrWhiteSpace(lsnBeforeDisconnect));
        }

        // Écrire un nouvel enregistrement pendant que le worker est hors-ligne
        int offlineEqId;
        using (var s = sp.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var offlineEq = new Equipement
            {
                SocieteId = 1,
                FamilleId = 1,
                Code = "OFFLINE-RESUME-01",
                Designation = "Pompe Creee Pendant Coupure Worker"
            };
            db.Equipements.Add(offlineEq);
            await db.SaveChangesAsync();
            offlineEqId = offlineEq.Id;
        }

        // Redémarrer le worker CDC et prouver la reprise exacte depuis le slot / checkpoint LSN
        using var cts2 = new CancellationTokenSource();
        var workerTask2 = Task.Run(() => cdcService.RunStreamingSessionAsync(cts2.Token));

        await WaitUntilAsync(async () =>
        {
            using var s = sp.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            return await db.CdcEvents.AnyAsync(c =>
                c.TableName == "Equipements" &&
                c.Operation == "INSERT" &&
                c.RecordId == offlineEqId.ToString());
        }, TimeSpan.FromSeconds(15), "Événement hors-ligne non rattrapé après redémarrage CDC.");

        using (var s = sp.CreateScope())
        {
            var redoSvc = s.ServiceProvider.GetRequiredService<IRedoLogService>();
            var overview = await redoSvc.GetOverviewAsync();
            Assert.Equal(slotName, overview.CdcWorker.SlotName);
            Assert.NotEqual(lsnBeforeDisconnect, overview.CdcWorker.LastConsumedLsn);
            Assert.True(overview.TotalCdcEvents >= 4);
            Assert.True(overview.TotalAuditEvents >= 4);

            // Vérifier zéro doublon LSN+Table+Op+RecordId
            var db = s.ServiceProvider.GetRequiredService<GmaoDbContext>();
            var dupCount = await db.CdcEvents
                .GroupBy(c => new { c.Lsn, c.TableName, c.Operation, c.RecordId })
                .CountAsync(g => g.Count() > 1);
            Assert.Equal(0, dupCount);

            // Aucun CdcEvent pour l'équipement qui a échoué (abortedCode)
            Assert.False(await db.CdcEvents.AnyAsync(c => c.NewValuesJson != null && c.NewValuesJson.Contains(abortedCode)));
        }

        cts2.Cancel();
        try { await workerTask2; } catch (OperationCanceledException) { }
    }

    private static async Task InitializeRealPostgresSchemaAsync(GmaoDbContext db, string connStr)
    {
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        await using (var checkCmd = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'AuditTrailEntries');", conn))
        {
            var exists = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);
            if (exists) return;
        }

        var script = db.Database.GenerateCreateScript();
        int firstInsertIdx = script.IndexOf("INSERT INTO \"", StringComparison.Ordinal);

        string ddlPart = firstInsertIdx >= 0 ? script[..firstInsertIdx] : script;
        string seedPart = firstInsertIdx >= 0 ? script[firstInsertIdx..] : string.Empty;

        await using (var ddlCmd = new NpgsqlCommand(ddlPart, conn))
        {
            await ddlCmd.ExecuteNonQueryAsync();
        }

        const string seedSocieteSql = @"
INSERT INTO ""Societes"" (""Id"", ""CodeTenant"", ""Nom"", ""SubscriptionPlan"", ""SubscriptionStatus"", ""SubscriptionAmount"", ""MaxSites"", ""MaxUsers"", ""MaxEquipements"", ""StorageUsedGb"", ""MaxStorageGb"", ""CapacityTonsPerDay"", ""IsActive"", ""CreatedAt"")
VALUES (1, 'capbon-cdc', 'Usine Concentre Cap Bon', 'Enterprise', 'Actif', 850, 3, 25, 150, 0, 50, 450, TRUE, NOW())
ON CONFLICT (""Id"") DO NOTHING;
SELECT setval(pg_get_serial_sequence('""Societes""', 'Id'), COALESCE((SELECT MAX(""Id"") FROM ""Societes""), 1));";

        await using (var socCmd = new NpgsqlCommand(seedSocieteSql, conn))
        {
            await socCmd.ExecuteNonQueryAsync();
        }

        if (!string.IsNullOrWhiteSpace(seedPart))
        {
            await using var seedCmd = new NpgsqlCommand(seedPart, conn);
            await seedCmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate, TimeSpan timeout, string failureMessage)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (await predicate()) return;
            await Task.Delay(150);
        }
        throw new TimeoutException(failureMessage);
    }
}
