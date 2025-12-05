using Microsoft.EntityFrameworkCore;
using Orderflow.Catalog.Data;
using Orderflow.Shared.Extensions;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add PostgreSQL DbContext
builder.AddNpgsqlDbContext<CatalogDbContext>("catalogdb");

// JWT Authentication (shared across all microservices)
builder.Services.AddJwtAuthentication(builder.Configuration);

// Register services
//builder.Services.AddScoped<ICategoryService, CategoryService>();
//builder.Services.AddScoped<IProductService, ProductService>();
//builder.Services.AddScoped<IStockService, StockService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-migrate database in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();

    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
