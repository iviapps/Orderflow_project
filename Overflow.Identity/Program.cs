using System.Reflection;                         // Necesario para Assembly.GetExecutingAssembly()
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;             // IdentityUser, IdentityRole, UserManager, RoleManager...
using Microsoft.EntityFrameworkCore;             // UseNpgsql, DbContext
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;                  // OpenApiInfo, OpenApiSecurityScheme...
using OrderFlow.Identity.Extensions;             // AddServiceDefaults, AddJwtAuthentication (tus extensiones)
using Overflow.Identity.Data;                    // AppDbContext

var builder = WebApplication.CreateBuilder(args);

// (1) Telemetría / health de Aspire
builder.AddServiceDefaults();                    // Expone health checks, métricas, etc. según tu ServiceDefaults

// (2) ConnectionString inyectada por Aspire (identitydb viene del AppHost)
var connStr = builder.Configuration
    .GetConnectionString("identitydb");          // Nombre debe cuadrar con el AppHost

// (3) DbContext con Npgsql apuntando a identitydb
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(connStr);                      // Usa Postgres como BBDD
});

// (4) ASP.NET Identity (usuarios + roles + EF Stores + tokens + SignInManager)
builder.Services
    .AddIdentityCore<IdentityUser>(opts =>
    {
        opts.User.RequireUniqueEmail = true;     // Impone email único por usuario
        // Aquí puedes alinear las reglas de Password con Identity si quieres
    })
    .AddRoles<IdentityRole>()                    // Soporte de roles (ADMIN, USER, etc.)
    .AddEntityFrameworkStores<AppDbContext>()    // Identity persiste en tu AppDbContext
    .AddDefaultTokenProviders()                  // Tokens para reset password, confirm email, etc.
    .AddSignInManager();                         // Inyecta SignInManager<IdentityUser> en DI

// (5) Autenticación JWT (usa tu extensión JwtAuthenticationExtensions)
builder.Services.AddJwtAuthentication(builder.Configuration); // Config JWT (Issuer, Audience, Key, etc.)

// (6) Autorización por roles / policies
builder.Services.AddAuthorization();             // Te permite usar [Authorize], roles, policies...

// (7) Infra básica API: controladores + FluentValidation
builder.Services.AddControllers();               // Activa controladores de Web API

builder.Services.AddFluentValidationAutoValidation();              // Ejecuta FluentValidation automáticamente
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
// Registra todos los AbstractValidator<T> del assembly

// (8) Swagger + esquema de seguridad Bearer
builder.Services.AddEndpointsApiExplorer();      // Necesario para exponer endpoints a Swagger

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo          // Define documento OpenAPI principal
    {
        Title = "OrderFlow.Identity",
        Version = "v1"
    });

    // Definición del esquema de seguridad Bearer (JWT en cabecera Authorization)
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Bearer. Usa: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", securityScheme); // Registro del esquema "Bearer"

    // Requisito global: Swagger conoce que se puede usar Bearer en todos los endpoints
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

// (9) Auto-migrar y seed de roles SOLO en Development - -      -   -   -   -   -   -   -   -   -   -   -   
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();     // Crea scope para resolver servicios de DI
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();                 // Aplica migraciones pendientes en desarrollo

    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    // Roles iniciales del sistema.
    // Incluyo ADMIN, USER y Customer porque los mencionabas en tus controladores.
    var roles = new[] { "ADMIN", "USER" };

    foreach (var role in roles)
    {
        if (!await roleMgr.RoleExistsAsync(role))     // Solo crea si no existe
        {
            await roleMgr.CreateAsync(new IdentityRole(role));
            Console.WriteLine($"[Seed] Rol creado: {role}");
        }
    }

    // Swagger UI solo en desarrollo
    app.UseSwagger();
    app.UseSwaggerUI();
}

//- -   -   -   -   -   -       -   -   -   -   -   -   -   -   -   -   -   -   -   --  

// (10) Middleware pipeline
app.UseHttpsRedirection();
app.UseCors();                         // Fuerza HTTPS (si aplica en tu entorno)
app.UseAuthentication();                              // Valida el JWT, rellena HttpContext.User
app.UseAuthorization();                               // Aplica [Authorize], roles, policies...

// (11) Endpoints de health / telemetría de Aspire
app.MapDefaultEndpoints();                            // Health checks / telemetry que registra ServiceDefaults

// (12) Rutas de controladores
app.MapControllers();                                 // Expone tus controladores de Web API

app.Run();                                            // Arranca la aplicación
