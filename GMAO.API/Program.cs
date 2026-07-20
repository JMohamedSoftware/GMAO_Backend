using System.Text;
using GMAO.Application.Interfaces;
using GMAO.Application.Services;
using GMAO.Infrastructure.Data;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

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

builder.Services.AddAuthorization();

// ── Controllers & API ────────────────────────────────────────────────────────
builder.Services.AddControllers(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});
builder.Services.AddEndpointsApiExplorer();



// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("GmaoPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod();
              // Note: AllowCredentials() removed — incompatible with wildcard origin
              // JWT is sent in Authorization header, not cookies, so not needed
    });
});

// ── Repositories & Services DI ────────────────────────────────────────────────
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEquipementRepository, EquipementRepository>();
builder.Services.AddScoped<IOrdreTravailRepository, OrdreTravailRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();

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
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Database migration failed: {ex.Message}");
    Console.WriteLine("The application will continue without migration.");
}

app.Run();
