using Asp.Versioning;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.OpenApi;
using Scalar.AspNetCore;

using Orderflow.Identity.Extensions;    // AddJwtAuthentication, OpenApiExtensions, DatabaseExtensions
using Orderflow.Identity.Services;
using Orderflow.Identity.Data;
using Orderflow.Identity.Services.Auth;
using Microsoft.Extensions.Hosting;       // IAuthService, AuthService

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();


// Configure OpenAPI documents for different versions with JWT Bearer authentication
builder.Services.AddOpenApi("v1", options =>
{
    options.ConfigureDocumentInfo(
        "Orderflow Identity API V1",
        "v1",
        "Authentication API using Minimal APIs with JWT Bearer authentication");
    options.AddJwtBearerSecurity();
    options.FilterByApiVersion("v1");
});

builder.Services.AddOpenApi("v2", options =>
{
    options.ConfigureDocumentInfo(
        "Orderflow Identity API V2",
        "v2",
        "Authentication API using Controllers with JWT Bearer authentication");
    options.AddJwtBearerSecurity();
    options.FilterByApiVersion("v2");
});



// ============================================
// AUTORIZACIÓN + CONTROLLERS
// ============================================
builder.Services.AddAuthorization();
builder.Services.AddControllers();

// ============================================
// API VERSIONING (Asp.Versioning)
// ============================================
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);     // Por defecto v1 (puedes poner 2,0 si tus controllers son v2)
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader(); // /api/v{version}/...
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";                   // v1, v2, v2.0...
        options.SubstituteApiVersionInUrl = true;
    });

// ============================================
// FLUENT VALIDATION
// ============================================
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ============================================
// CORS CONFIGURATION
// ============================================
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ============================================
// DATABASE (PostgreSQL)
// ============================================

var connStr = builder.Configuration.GetConnectionString("identitydb");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connStr);
});

//============================================
//MASS TRANSIT + RABBITMQ(pendiente de consumers)
//============================================
// Configure MassTransit with RabbitMQ for event publishing
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var configuration = context.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("messaging");

        if (!string.IsNullOrEmpty(connectionString))
        {
            cfg.Host(new Uri(connectionString));
        }

        cfg.ConfigureEndpoints(context);
    });
});

// ============================================
// ASP.NET CORE IDENTITY config
// ============================================
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;

    // Sign in settings
    options.SignIn.RequireConfirmedEmail = false; // En producción normalmente true
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ============================================
// SERVICE LAYER
// ============================================
builder.Services.AddScoped<IAuthService, AuthService>();     
builder.Services.AddScoped<ITokenService, TokenService>();
//NOT YET IMPLEMENTED
// builder.Services.AddScoped<IUserService, UserService>();
// builder.Services.AddScoped<IRoleService, RoleService>();

// ============================================
// JWT BEARER AUTHENTICATION
// ============================================
builder.Services.AddJwtAuthentication(builder.Configuration); // Tu extensión JWT

var app = builder.Build();

// ============================================
// SEED DEVELOPMENT DATA (DB, Roles, Admin User)
// ============================================
if (app.Environment.IsDevelopment())
{
    // Migraciones + roles + admin
    await app.Services.SeedDevelopmentDataAsync();

    // Documentos OpenAPI (usa los nombres del AddOpenApi)
    app.MapOpenApi(); // expone /openapi/v1.json

    // Scalar UI
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Orderflow Identity API")
            .AddDocument("v1", "V1 - Controllers", "/openapi/v1.json", isDefault: true);

        // Cuando tengas "v2":
        // .AddDocument("v2", "V2 - Controllers", "/openapi/v2.json");
    });

    // Swagger UI opcional (si tienes Swashbuckle). Puedes eliminarlo cuando te quedes solo con Scalar.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Orderflow Identity API V1");
        // options.SwaggerEndpoint("/openapi/v2.json", "Orderflow Identity API V2");
    });
}

app.UseHttpsRedirection();

app.UseCors();                // Siempre antes de Auth / AuthZ

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();    // Health checks / telemetry Aspire

app.MapControllers();         // Solo controllers

await app.RunAsync();
