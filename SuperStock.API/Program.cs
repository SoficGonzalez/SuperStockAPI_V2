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

// ── Validar configuración requerida ─────────────────────────────────────
var jwtKey = builder.Configuration["JWTKey"]
    ?? throw new InvalidOperationException("JWTKey no esta configurado en appsettings.json.");

var jwtIssuer = builder.Configuration["JWTIssuer"]
    ?? throw new InvalidOperationException("JWTIssuer no esta configurado en appsettings.json.");

// ── MongoDB (reemplaza builder.Services.AddDbContext<ApplicationDbContext>) ──
// En Tickets, aquí se configuraba EF Core + SQL Server:
//   builder.Services.AddDbContext<ApplicationDbContext>(options =>
//       options.UseSqlServer(connectionString));
//
// Ahora se registra MongoDbSettings y MongoDbContext como Singleton.
// Singleton porque MongoClient internamente maneja un pool de conexiones
// y es thread-safe (a diferencia de EF DbContext que es Scoped).
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDb"));
builder.Services.AddSingleton<MongoDbContext>();

// ── JWT Authentication (mismo patrón que Tickets) ───────────────────────
// Esta sección es idéntica al proyecto original. JWT no depende de
// la base de datos; solo necesita la clave simétrica para firmar/verificar tokens.
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

// ── Swagger (mismo patrón que Tickets) ──────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SuperStock SV API",
        Version = "v1",
        Description = "API REST para gestion de inventario de supermercados con MongoDB"
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

// ── Dependency Injection ────────────────────────────────────────────────
// En Tickets, aquí se registraban:
//   builder.Services.AddScoped<IUserRepository, UserRepository>();    → ahora IUsuarioRepository
//   builder.Services.AddScoped<IRoleRepository, RoleRepository>();    → eliminado (rol es campo del usuario)
//   builder.Services.AddScoped<ITicketsRepository, TicketsRepository>(); → ahora IProductoRepository, etc.
//
// Repositories son Scoped (uno por request HTTP), mismo patrón que Tickets.
builder.Services.AddScoped<IProductoRepository, ProductoRepository>();
builder.Services.AddScoped<IVentaRepository, VentaRepository>();
builder.Services.AddScoped<IProveedorRepository, ProveedorRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();

// Application services (misma estructura que AuthService y TicketsCase)
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProductoService>();
builder.Services.AddScoped<VentaService>();
builder.Services.AddScoped<ProveedorService>();

// Infrastructure services
builder.Services.AddScoped<IJWTService, JWTService>();

var app = builder.Build();

// ── Middleware Pipeline (mismo orden que Tickets) ────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Seed Data (reemplaza SeedDataAsync de Tickets) ──────────────────────
// En Tickets, el seed creaba roles "Admin"/"User" via RoleManager
// y un usuario admin via UserManager (ASP.NET Identity).
// Aquí hacemos lo mismo pero directo en MongoDB con BCrypt.
await SeedDataAsync(app);

app.Run();

/// <summary>
/// Inicializa la base de datos: crea índices, aplica Schema Validation,
/// y siembra el usuario administrador inicial.
///
/// CONCEPTOS APLICADOS:
///   - Índices secundarios: se crean en InitializeAsync para optimizar consultas.
///   - Schema Validation ($jsonSchema): se aplica vía collMod para garantizar estructura mínima.
///   - TTL Index: se configura para limpiar automáticamente ventas anuladas tras 90 días.
///   - Write Concern / Read Concern: configurados a nivel de MongoClient en MongoDbContext.
/// </summary>
static async Task SeedDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
    var usuarioRepo = scope.ServiceProvider.GetRequiredService<IUsuarioRepository>();

    // Crear índices y aplicar Schema Validation
    await context.InitializeAsync();

    // Crear usuario admin si no existe (mismo patrón que Tickets)
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
