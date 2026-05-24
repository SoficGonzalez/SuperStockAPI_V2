using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using SuperStock.Application.Services;
using SuperStock.Domain.Interfaces;
using SuperStock.Domain.Interfaces.Repositories;
using SuperStock.Infrastructure.Persistence;
using SuperStock.Infrastructure.Persistence.Repositories;
using SuperStock.Infrastructure.Security;
using SuperStock.Infrastructure.Settings;
using SuperStock.Domain.Entities;



var builder = WebApplication.CreateBuilder(args);

// ── Validar configuracion JWT ───────────────────────────────────────
var jwtKey = builder.Configuration["JWTKey"]
    ?? throw new InvalidOperationException("JWTKey no esta configurado en appsettings.json.");

var jwtIssuer = builder.Configuration["JWTIssuer"]
    ?? throw new InvalidOperationException("JWTIssuer no esta configurado en appsettings.json.");

// ── Cassandra (reemplaza MongoDB) ────────────────────────────────────
// CassandraDbContext es Singleton porque el driver maneja internamente
// un pool de conexiones thread-safe (igual que MongoClient).
builder.Services.Configure<CassandraSettings>(
    builder.Configuration.GetSection("Cassandra"));
builder.Services.AddSingleton<CassandraDbContext>();

// ── JWT Authentication ──────────────────────────────────────────────
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();

// ── Swagger ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SuperStock SV API (Cassandra)",
        Version = "v1",
        Description = "API REST para gestion de inventario sobre cluster Cassandra"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Ingrese el token JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(document =>
    {
        return new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        };
    });
});

// ── Dependency Injection ────────────────────────────────────────────
// Repositorios Cassandra (scoped: uno por request HTTP)
builder.Services.AddScoped<IProductoRepository, ProductoRepository>();
builder.Services.AddScoped<IVentaRepository, VentaRepository>();
builder.Services.AddScoped<IProveedorRepository, ProveedorRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();

// Application services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProductoService>();
builder.Services.AddScoped<VentaService>();
builder.Services.AddScoped<ProveedorService>();

// Infrastructure services
builder.Services.AddScoped<IJWTService, JWTService>();

var app = builder.Build();

// ── Middleware Pipeline ─────────────────────────────────────────────
// Habilitamos Swagger en todos los entornos para facilitar pruebas en Docker.
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Inicializacion de Cassandra con reintentos ─────────────────────
// El cluster puede tardar en estar listo en Docker; reintentamos hasta 12 veces.
await InitializeCassandraWithRetryAsync(app);

// ── Seed Data ───────────────────────────────────────────────────────
await SeedDataAsync(app);

app.Run();

static async Task InitializeCassandraWithRetryAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CassandraDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    const int maxRetries = 12;
    const int delaySeconds = 10;

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await context.InitializeAsync();
            logger.LogInformation("Cassandra inicializada correctamente en el intento {Attempt}.", attempt);
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Cassandra no responde aun (intento {Attempt}/{Max}). Reintentando en {Delay}s...",
                attempt, maxRetries, delaySeconds);

            if (attempt == maxRetries)
                throw;

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }
    }
}

static async Task SeedDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var usuarioRepo = scope.ServiceProvider.GetRequiredService<IUsuarioRepository>();

    const string adminEmail = "admin@superstock.sv";
    const string adminPassword = "Admin123!";

    if (!await usuarioRepo.ExistsByEmailAsync(adminEmail))
    {
        var adminUser = new Usuario
        {
            Nombre = "Admin",
            Apellido = "SuperStock",
            Email = adminEmail,
            Telefono = "",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            Rol = "admin",
            Sucursal = "principal",
            Activo = true
        };

        await usuarioRepo.AddAsync(adminUser);
        Console.WriteLine($"[Seed] Usuario admin creado: {adminEmail} / {adminPassword}");
    }
}
