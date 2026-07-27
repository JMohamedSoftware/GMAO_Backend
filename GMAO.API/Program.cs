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
        policy.WithOrigins(
            "http://localhost:5173", 
            "https://gmao-frontend.vercel.app", 
            "https://g-m-a-o.vercel.app",
            "https://gmao-saas.com" // future domain
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", app = "GMAO API", timestamp = DateTime.UtcNow }));

// ── Auto-migration et Seeding au démarrage ───────────────────
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GmaoDbContext>();
    db.Database.Migrate();
    Console.WriteLine("✅ Database migration completed successfully.");

    // Seeding demo data
    if (!db.Localisations.Any(l => l.Nom == "05 - Usine Tomates POMODORO"))
    {
        Console.WriteLine("🌱 Seeding Demo Data...");
        var root = new GMAO.Domain.Entities.Localisation { Nom = "05 - Usine Tomates POMODORO" };
        db.Localisations.Add(root);
        db.SaveChanges();

        var l1 = new GMAO.Domain.Entities.Localisation { Nom = "1 - Réception & Lavage", ParentId = root.Id };
        var l2 = new GMAO.Domain.Entities.Localisation { Nom = "2 - Concentration", ParentId = root.Id };
        var l3 = new GMAO.Domain.Entities.Localisation { Nom = "3 - Conditionnement & Stérilisation", ParentId = root.Id };
        var l4 = new GMAO.Domain.Entities.Localisation { Nom = "4 - Énergie & Utilités", ParentId = root.Id };

        db.Localisations.AddRange(l1, l2, l3, l4);
        db.SaveChanges();

        var e1 = new GMAO.Domain.Entities.Equipement { Id = "EQ-CONV-001", Name = "Convoyeur à bande Réception", Category = "Production", Criticality = "Haute", Status = "En service", HealthIndex = 85, LocalisationId = l1.Id, SerialNumber = "SN-001", Brand = "Buhler", CommissionDate = DateTime.UtcNow };
        var e2 = new GMAO.Domain.Entities.Equipement { Id = "EQ-EVAP-001", Name = "Évaporateur Concentrateur N°1", Category = "Production", Criticality = "Critique", Status = "En service", HealthIndex = 70, LocalisationId = l2.Id, SerialNumber = "SN-002", Brand = "Alfa Laval", CommissionDate = DateTime.UtcNow };
        var e3 = new GMAO.Domain.Entities.Equipement { Id = "EQ-PUMP-001", Name = "Pompe Centrifuge LKH-25", Category = "Production", Criticality = "Moyenne", Status = "En panne", HealthIndex = 30, LocalisationId = l2.Id, SerialNumber = "SN-003", Brand = "Alfa Laval", CommissionDate = DateTime.UtcNow };
        var e4 = new GMAO.Domain.Entities.Equipement { Id = "EQ-AUTO-001", Name = "Autoclave FMC Steril-Host 4", Category = "Production", Criticality = "Critique", Status = "En maintenance", HealthIndex = 50, LocalisationId = l3.Id, SerialNumber = "SN-004", Brand = "FMC", CommissionDate = DateTime.UtcNow };
        var e5 = new GMAO.Domain.Entities.Equipement { Id = "EQ-BOIL-001", Name = "Chaudière Thermique Babcock VAP 3000", Category = "Utilités", Criticality = "Critique", Status = "En service", HealthIndex = 95, LocalisationId = l4.Id, SerialNumber = "SN-005", Brand = "Babcock", CommissionDate = DateTime.UtcNow };

        db.Equipements.AddRange(e1, e2, e3, e4, e5);
        db.SaveChanges();
        Console.WriteLine("✅ Seeding completed successfully.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Database migration failed: {ex.Message}");
    Console.WriteLine("The application will continue without migration.");
}

app.Run();
