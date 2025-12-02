using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orderflow.Identity.Data;

namespace Orderflow.Identity.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Seeds the database with initial development data including roles and admin user
    /// Extension avialable only in Development environment and program.cs 
    /// </summary>
    public static async Task SeedDevelopmentDataAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        // Run migrations
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();

        // Seed roles
        await SeedRolesAsync(services);

        // Seed admin user
        await SeedAdminUserAsync(services);
    }

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in Roles.GetAll())
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                Console.WriteLine($"✅ Role created: {role}");
            }
        }
    }

    private static async Task SeedAdminUserAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        const string adminEmail = "admin@admin.com";
        const string adminPassword = "Test12345.";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);

            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, Roles.Admin);
                Console.WriteLine($"✅ Admin user created: {adminEmail}");
            }
            else
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                Console.WriteLine($"❌ Failed to create admin user: {errors}");
            }
        }
        else
        {
            Console.WriteLine($"ℹ️  Admin user already exists: {adminEmail}");
        }
    }
}
