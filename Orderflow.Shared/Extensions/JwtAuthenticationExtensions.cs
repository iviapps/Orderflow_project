using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Orderflow.Shared.Extensions;

/// <summary>   
/// Shared JWT Bearer authentication configuration for microservices.
/// All services use the same JWT secret, issuer, and audience to validate tokens.
/// </summary>
public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Adds JWT Bearer authentication with standard configuration.
    /// Requires Jwt:Secret, Jwt:Issuer, and Jwt:Audience in configuration.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration containing JWT settings</param>
    /// <param name="configureEvents">Optional action to configure JWT Bearer events</param>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<JwtBearerEvents>? configureEvents = null)
    {
        // Use fallback values for build-time/design-time scenarios
        var jwtSecret = configuration["Jwt:Secret"]
            ?? "build-time-secret-key-minimum-32-characters-required-for-hmac-sha256";
        var jwtIssuer = configuration["Jwt:Issuer"]
            ?? "build-time-issuer";
        var jwtAudience = configuration["Jwt:Audience"]
            ?? "build-time-audience";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero, // Remove default 5 minute tolerance
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };

            // Allow custom event configuration
            if (configureEvents != null)
            {
                options.Events = new JwtBearerEvents();
                configureEvents(options.Events);
            }
        });

        services.AddAuthorization();

        return services;
    }
}
