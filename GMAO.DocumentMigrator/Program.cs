using System.Text.Json;
using GMAO.Application.Interfaces;
using GMAO.Infrastructure.Data;
using GMAO.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// =============================================================================
// GMAO.DocumentMigrator — Utilitaire CLI de migration documentaire sécurisée
// Disque local (wwwroot/uploads) -> Supabase Storage privé (gmao-documents)
//
// Usages :
//   dotnet run -- --dry-run [--wwwroot ./wwwroot] [--societe 1] [--limit 50]
//   dotnet run -- --execute [--wwwroot ./wwwroot] [--manifest ./manifest.json]
//   dotnet run -- --rollback ./manifest.json [--wwwroot ./wwwroot]
// =============================================================================

var execute = args.Contains("--execute", StringComparer.OrdinalIgnoreCase);
var rollbackManifest = GetArgValue(args, "--rollback");
var webRoot = GetArgValue(args, "--wwwroot")
    ?? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "GMAO.API", "wwwroot"));
var manifestOut = GetArgValue(args, "--manifest");
var societeFilter = int.TryParse(GetArgValue(args, "--societe"), out var sid) ? sid : (int?)null;
var limit = int.TryParse(GetArgValue(args, "--limit"), out var lim) ? lim : (int?)null;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "..", "GMAO.API", "appsettings.json"), optional: true)
    .AddEnvironmentVariables()
    .Build();

var connStr = Environment.GetEnvironmentVariable("GMAO_MIGRATOR_CONNECTION_STRING")
    ?? config.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connStr))
{
    Console.Error.WriteLine("[ERREUR] Chaîne de connexion PostgreSQL manquante (ConnectionStrings__DefaultConnection ou GMAO_MIGRATOR_CONNECTION_STRING).");
    return 2;
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o =>
{
    o.TimestampFormat = "HH:mm:ss ";
    o.SingleLine = true;
}));
services.AddSingleton<ITenantContextService, SystemMigrationTenantContext>();
services.Configure<SupabaseStorageOptions>(config.GetSection(SupabaseStorageOptions.SectionName));
services.AddHttpClient<SupabaseFileStorageService>();
services.AddDbContext<GmaoDbContext>((sp, opts) =>
{
    opts.UseNpgsql(connStr);
});

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<GmaoDbContext>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<LegacyDocumentMigrationRunner>>();
var supabaseOpts = scope.ServiceProvider.GetRequiredService<IOptions<SupabaseStorageOptions>>().Value;

IFileStorageService? targetStorage = null;
if (execute)
{
    if (!supabaseOpts.IsConfigured)
    {
        Console.Error.WriteLine("[ERREUR] Mode --execute impossible : Supabase__Url et Supabase__ServiceRoleKey ne sont pas configurés via variables d'environnement.");
        return 3;
    }

    var supa = scope.ServiceProvider.GetRequiredService<SupabaseFileStorageService>();
    await supa.EnsurePrivateBucketAsync();
    targetStorage = supa;
}

var runner = new LegacyDocumentMigrationRunner(db, targetStorage, logger);

if (!string.IsNullOrWhiteSpace(rollbackManifest))
{
    Console.WriteLine($"[ROLLBACK] Restauration des métadonnées depuis : {rollbackManifest}");
    var (restored, skipped) = await runner.RollbackFromManifestAsync(rollbackManifest, webRoot);
    Console.WriteLine($"[ROLLBACK] Terminé : {restored} document(s) restauré(s), {skipped} ignoré(s).");
    return 0;
}

var report = await runner.RunAsync(new LegacyMigrationOptions
{
    Execute = execute,
    WebRootPath = webRoot,
    FilterSocieteId = societeFilter,
    Limit = limit,
    ManifestOutputPath = manifestOut
});

Console.WriteLine("=== RAPPORT DE MIGRATION DOCUMENTAIRE GMAO ===");
Console.WriteLine($"Mode                     : {report.Mode}");
Console.WriteLine($"WebRoot inspecté         : {report.WebRootPath}");
Console.WriteLine($"Total Documents BDD      : {report.TotalDocumentsInDb}");
Console.WriteLine($"Déjà sur Supabase        : {report.AlreadySupabaseCount}");
Console.WriteLine($"Éligibles (Local)        : {report.ReadyToMigrateCount}");
Console.WriteLine($"Migrés & vérifiés SHA256 : {report.MigratedCount}");
Console.WriteLine($"Fichiers absents disque  : {report.MissingPhysicalFileCount}");
Console.WriteLine($"Tenants non résolus      : {report.UnresolvedTenantCount}");
Console.WriteLine($"Fichiers orphelins       : {report.OrphanPhysicalFiles.Count}");
Console.WriteLine($"Photos locales héritées  : {report.LegacyLocalPhotoReferences.Count}");
Console.WriteLine($"Erreurs                  : {report.ErrorCount}");

if (!string.IsNullOrWhiteSpace(report.ManifestFilePath))
    Console.WriteLine($"Manifeste de rollback    : {report.ManifestFilePath}");

var reportOutPath = Path.Combine(Directory.GetCurrentDirectory(), $"migration-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
await File.WriteAllTextAsync(reportOutPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Rapport JSON détaillé    : {reportOutPath}");

return report.ErrorCount > 0 ? 1 : 0;

static string? GetArgValue(string[] args, string flag)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }
    return null;
}
