using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.Identity.Extensions;
using Overflow.Identity.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);



// (1) Telemetría/health/endpoints de Aspire
builder.AddServiceDefaults();

// (2) ConnectionString inyectada por Aspire en runtime
var connStr = builder.Configuration.GetConnectionString("identitydb");

// (3) DbContext con Npgsql
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(connStr);
});

// (4) ASP.NET Identity (Core + Roles + EF Stores)
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
    .AddDefaultTokenProviders();
//SECRETS

//builder.Configuration.AddUserSecrets<Program>();

//FALTA HACER LLAMADA A JWT 
// (5) Autenticación JWT Bearer
//EXTENSIONS>JWTAUTENTHICATIONEXTENSIONS 
//currently using using OrderFlow.Identity.Extensions; <-  JwtAuthenticationExtensions modified by " public static IServiceCollection AddJwtAuthentication.." <- 
//es un metodo extendido con this IServiceCollection services    <- lo que lo hace posible de exportar e inyectar en mi program 
builder.Services.AddJwtAuthentication(builder.Configuration);   

builder.Services.AddAuthorization();

// (6) Infra básica API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// (7) Auto-migrar y seed SOLO en Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync(); // aplica migraciones pendientes
    
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    // Define aquí los roles iniciales
    var roles = new[] { "ADMIN", "USER" };
    foreach (var role in roles)
    {
        if (!await roleMgr.RoleExistsAsync(role))
        {
            await roleMgr.CreateAsync(new IdentityRole(role));
            Console.WriteLine($"[Seed] Rol creado: {role}");
        }
    }
    
    // Swagger solo en Development
    app.UseSwagger();
    app.UseSwaggerUI();

    
}

// (8) Middleware pipeline
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// (9) Endpoints de OpenAPI y health/telemetría de Aspire
//app.mapopenapi <- no lo uso 
app.MapDefaultEndpoints();

// (10) Rutas de controladores
app.MapControllers();

app.Run();