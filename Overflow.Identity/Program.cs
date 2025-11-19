using System.Reflection;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OrderFlow.Identity.Extensions;        // AddJwtAuthentication
using Overflow.Identity.Data;               // AppDbContext

var builder = WebApplication.CreateBuilder(args);

// (1) Telemetría / health de Aspire
builder.AddServiceDefaults();

// (2) ConnectionString inyectada por Aspire (identitydb viene del AppHost)
var connStr = builder.Configuration.GetConnectionString("identitydb");

// (3) DbContext con Npgsql apuntando a identitydb
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(connStr);
});

// (4) ASP.NET Identity (usuarios + roles + EF Stores + SignInManager)
builder.Services
    .AddIdentityCore<IdentityUser>(opts =>
    {
        opts.User.RequireUniqueEmail = true;
        opts.Password.RequiredLength = 6;
        opts.Password.RequireNonAlphanumeric = false;
        opts.Password.RequireUppercase = false;
        opts.Password.RequireLowercase = true;
        opts.Password.RequireDigit = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddSignInManager(); // ? registra SignInManager<IdentityUser> en DI

// (5) Autenticación JWT (usa tu extensión JwtAuthenticationExtensions)
builder.Services.AddJwtAuthentication(builder.Configuration);

// (6) Autorización por políticas / roles
builder.Services.AddAuthorization();

// (7) Infra básica API: controladores + FluentValidation (opcional pero coherente con tu controller)
builder.Services.AddControllers();

// Si usas IValidator<T> en el controller, activa esto:
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// (8) Swagger + esquema de seguridad Bearer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OrderFlow.Identity",
        Version = "v1"
    });

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

    c.AddSecurityDefinition("Bearer", securityScheme);

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

// (9) Auto-migrar y seed de roles SOLO en Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync(); // Aplica migraciones pendientes

    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    // Define los roles iniciales. Asegúrate de incluir "Customer" si lo usas en el controller.
    var roles = new[] { "ADMIN", "USER", "Customer" };

    foreach (var role in roles)
    {
        if (!await roleMgr.RoleExistsAsync(role))
        {
            await roleMgr.CreateAsync(new IdentityRole(role));
            Console.WriteLine($"[Seed] Rol creado: {role}");
        }
    }

    // Swagger UI solo en desarrollo
    app.UseSwagger();
    app.UseSwaggerUI();
}

// (10) Middleware pipeline
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// (11) Mapear controladores de Web API
app.MapControllers();

// (12) Endpoints de health / telemetría de Aspire
app.MapDefaultEndpoints();

app.Run();
