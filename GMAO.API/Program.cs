using System.Text;
using GMAO.Application.Interfaces;
using GMAO.Application.Services;
using GMAO.Infrastructure.Data;
using GMAO.Infrastructure.Services;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Désactiver le rechargement à chaud des configs pour éviter les Segmentation Faults (139) sur Render
Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");

var builder = WebApplication.CreateBuilder(args);

// ── IHttpContextAccessor (requis pour le Multi-Tenant dans GmaoDbContext) ────
builder.Services.AddHttpContextAccessor();

// ── Base de données PostgreSQL ───────────────────────────────────────────────
builder.Services.AddDbContext<GmaoDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.MigrationsAssembly("GMAO.Infrastructure")
    )
);

// ── Authentification JWT ─────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey manquant");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    // Define policies for generic actions
    options.AddPolicy("WorkOrderCreate", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("Permission", "WORKORDER_CREATE") ||
        context.User.HasClaim("Permission", "WORKORDER_CREATE_ALL") ||
        context.User.HasClaim("Permission", "WORKORDER_CREATE_TEAM") ||
        context.User.HasClaim("Permission", "WORKORDER_CREATE_OWN") ||
        context.User.IsInRole("SuperAdmin") ||
        context.User.IsInRole("Administrateur")));

    options.AddPolicy("WorkOrderUpdate", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("Permission", "WORKORDER_UPDATE") ||
        context.User.HasClaim("Permission", "WORKORDER_UPDATE_ALL") ||
        context.User.HasClaim("Permission", "WORKORDER_UPDATE_TEAM") ||
        context.User.HasClaim("Permission", "WORKORDER_UPDATE_OWN") ||
        context.User.IsInRole("SuperAdmin") ||
        context.User.IsInRole("Administrateur")));
});

// ── Controllers & API ────────────────────────────────────────────────────────
builder.Services.AddControllers(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddEndpointsApiExplorer();



// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("GmaoPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── Repositories & Services DI ────────────────────────────────────────────────
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEquipementRepository, EquipementRepository>();
builder.Services.AddScoped<IOrdreTravailRepository, OrdreTravailRepository>();
builder.Services.AddScoped<IEquipeRepository, EquipeRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<IEquipeService, EquipeService>();
builder.Services.AddScoped<ILocalisationService, LocalisationService>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "GMAO API", Version = "v1" });
});

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "GMAO API v1");
    c.RoutePrefix = string.Empty; // Swagger UI accessible at root URL '/'
});

app.UseCors("GmaoPolicy");

var webRootPath = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var uploadsPath = Path.Combine(webRootPath, "uploads", "documents");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", app = "GMAO API", timestamp = DateTime.UtcNow }));

// ── Auto-migration et Seeding au démarrage ───────────────────
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GmaoDbContext>();

    try
    {
        db.Database.ExecuteSqlRaw(@"
            DO $$ 
            BEGIN 
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Localisations' AND column_name='SocieteId') THEN
                    ALTER TABLE ""Localisations"" ADD COLUMN ""SocieteId"" integer NOT NULL DEFAULT 1;
                    BEGIN
                        ALTER TABLE ""Localisations"" ADD CONSTRAINT ""FK_Localisations_Societes_SocieteId"" 
                            FOREIGN KEY (""SocieteId"") REFERENCES ""Societes"" (""Id"") ON DELETE CASCADE;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END IF;

                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Equipements' AND column_name='LocalisationId' AND is_nullable='NO') THEN
                    ALTER TABLE ""Equipements"" ALTER COLUMN ""LocalisationId"" DROP NOT NULL;
                    BEGIN
                        ALTER TABLE ""Equipements"" DROP CONSTRAINT IF EXISTS ""FK_Equipements_Localisations_LocalisationId"";
                        ALTER TABLE ""Equipements"" ADD CONSTRAINT ""FK_Equipements_Localisations_LocalisationId"" 
                            FOREIGN KEY (""LocalisationId"") REFERENCES ""Localisations"" (""Id"") ON DELETE SET NULL;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END;
                END IF;

                BEGIN
                    DROP INDEX IF EXISTS ""IX_Fournisseurs_Code"";
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Fournisseurs_SocieteId_Code"" ON ""Fournisseurs"" (""SocieteId"", ""Code"");
                EXCEPTION WHEN OTHERS THEN NULL;
                END;

                UPDATE ""Equipements"" SET ""Etat"" = 1 WHERE ""Etat"" = 0 OR ""Etat"" IS NULL;
                UPDATE ""Equipements"" SET ""Criticite"" = 2 WHERE ""Criticite"" = 0 OR ""Criticite"" IS NULL;
            END $$;
        ");
    }
    catch (Exception sqlEx)
    {
        Console.WriteLine($"⚠️ Direct SQL schema update warning: {sqlEx.Message}");
    }

    db.Database.Migrate();
    Console.WriteLine("✅ Database migration completed successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Database migration failed: {ex.Message} -> {ex.InnerException?.Message}");
    Console.WriteLine("The application will continue without migration.");
}

app.Run();
