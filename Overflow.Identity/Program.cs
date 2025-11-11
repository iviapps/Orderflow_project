using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Overflow.Identity.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 1) ConnectionString inyectada por Aspire (.WithReference("identitydb"))
var connStr = builder.Configuration.GetConnectionString("identitydb");

// 2) DbContext con Npgsql
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(connStr);
});

// 3) Identity Core + Roles + EF Stores
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
    .AddRoles<IdentityRole>() // habilita tabla AspNetRoles
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// 4) Autenticación JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-key-32chars-min-xxxxxxxxxxxxxxxxxxxxxxx";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Overflow.Identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Overflow.ApiClients";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//migrations con scoped 
// 5) Aplicar migraciones en arranque (útil en desarrollo)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate(); // crea/actualiza esquema si faltan migraciones aplicadas
}

app.MapDefaultEndpoints();

// --- 5) CREAR ROLES POR DEFECTO (ADMIN / MEMBER) ---
using (var scope = app.Services.CreateScope())
{
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var roles = new[] { "ADMIN", "MEMBER" };

    foreach (var role in roles)
    {
        if (!await roleMgr.RoleExistsAsync(role))
        {
            await roleMgr.CreateAsync(new IdentityRole(role));
            Console.WriteLine($"[Seed] Rol creado: {role}");
        }
    }
}

// ---------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
    