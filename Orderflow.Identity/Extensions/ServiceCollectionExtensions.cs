using System.Reflection;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orderflow.Identity.Data;
using Orderflow.Identity.Extensions; // si aquí tienes AddJwtAuthentication

namespace Orderflow.Identity.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddIdentityInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // (2) ConnectionString identitydb
            var connStr = configuration.GetConnectionString("identitydb");

            // (3) DbContext con Npgsql
            services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseNpgsql(connStr);
            });

            // (4) Identity (usuarios + roles + EF Stores + tokens + SignInManager)
            services
                .AddIdentityCore<IdentityUser>(opts =>
                {
                    opts.User.RequireUniqueEmail = true;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders()
                .AddSignInManager();

            // (5) JWT + autorización
            services.AddJwtAuthentication(configuration);
            services.AddAuthorization();

            // (7) Infra básica API: controladores + FluentValidation
            services.AddControllers();

            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            // NO se registran aquí servicios de dominio/aplicación (AuthService, UserService, etc.)
            return services;
        }
    }
}
