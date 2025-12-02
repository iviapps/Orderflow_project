using Microsoft.AspNetCore.Identity;

namespace Orderflow.Identity.Services.Auth;

/// <summary>
/// Service for generating and managing JWT tokens
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token for an authenticated user
    /// </summary>
    /// <param name="user">The authenticated user</param>
    /// <param name="roles">User's roles to include in token claims</param>
    /// <returns>JWT token string</returns>
    Task<string> GenerateAccessTokenAsync(IdentityUser user, IEnumerable<string> roles);

    /// <summary>
    /// Gets the token expiry time in seconds
    /// </summary>
    /// <returns>Token expiry in seconds</returns>
    int GetTokenExpiryInSeconds();
}