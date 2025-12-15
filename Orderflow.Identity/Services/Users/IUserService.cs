using Orderflow.Identity.DTOs.Users.Queries;
using Orderflow.Identity.DTOs.Users.Requests;
using Orderflow.Identity.DTOs.Users.Responses;
using Orderflow.Shared.Common;


namespace Orderflow.Identity.Services.Users;

/// <summary>
/// Service for user management operations
/// </summary>
public interface IUserService
{
    // List & Search
    /// <summary>
    /// Get paginated list of users with filtering and sorting
    /// </summary>
    Task<PaginatedResult<UserResponse>> GetUsersAsync(UserQueryParameters parameters);

    // Individual User Operations
    /// <summary>
    /// Get user details by ID
    /// </summary>
    Task<ServiceResult<UserDetailResponse>> GetUserByIdAsync(string userId);

    /// <summary>
    /// Create a new user (admin operation)
    /// </summary>
    Task<ServiceResult<UserResponse>> CreateUserAsync(CreateUserRequest request);

    /// <summary>
    /// Update user information (admin operation)
    /// </summary>
    Task<ServiceResult<UserResponse>> UpdateUserAsync(string userId, UpdateUserRequest request);

    /// <summary>
    /// Delete user account (admin operation)
    /// </summary>
    Task<ServiceResult> DeleteUserAsync(string userId);

    // Account Management
    /// <summary>
    /// Lock user account to prevent login
    /// </summary>
    Task<ServiceResult> LockUserAsync(string userId, DateTimeOffset? lockoutEnd);

    /// <summary>
    /// Unlock user account
    /// </summary>
    Task<ServiceResult> UnlockUserAsync(string userId);

    // Profile Management (Self)
    /// <summary>
    /// Get current user's full profile
    /// </summary>
    Task<ServiceResult<UserDetailResponse>> GetCurrentUserProfileAsync(string userId);

    /// <summary>
    /// Update current user's profile
    /// </summary>
    Task<ServiceResult<UserResponse>> UpdateCurrentUserProfileAsync(string userId, UpdateProfileRequest request);

    /// <summary>
    /// Change user's password
    /// </summary>
    Task<ServiceResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword);

    // Role Assignment
    /// <summary>
    /// Get user's roles
    /// </summary>
    Task<ServiceResult<IEnumerable<string>>> GetUserRolesAsync(string userId);

    /// <summary>
    /// Assign role to user
    /// </summary>
    Task<ServiceResult> AddUserToRoleAsync(string userId, string roleName);

    /// <summary>
    /// Remove role from user
    /// </summary>
    Task<ServiceResult> RemoveUserFromRoleAsync(string userId, string roleName);
}
