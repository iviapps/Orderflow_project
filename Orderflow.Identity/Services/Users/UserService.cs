using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orderflow.Identity.DTOs.Users.Queries;
using Orderflow.Identity.DTOs.Users.Requests;
using Orderflow.Identity.DTOs.Users.Responses;
using Orderflow.Shared.Common;



namespace Orderflow.Identity.Services.Users;

/// <summary>
/// Service for user management operations
/// </summary>
public class UserService : IUserService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<IdentityUser> userManager,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated list of users with filtering and sorting
    /// </summary>
    public async Task<PaginatedResult<UserResponse>> GetUsersAsync(UserQueryParameters parameters)
    {
        // Start with all users query
        var query = _userManager.Users.AsQueryable();

        // Apply search filter (email or username)
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var searchLower = parameters.Search.ToLower();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                (u.UserName != null && u.UserName.ToLower().Contains(searchLower)));
        }

        // Apply role filter if specified
        if (!string.IsNullOrWhiteSpace(parameters.Role))
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(parameters.Role);
            var userIds = usersInRole.Select(u => u.Id).ToList();
            query = query.Where(u => userIds.Contains(u.Id));
        }

        // Apply sorting (default to email ascending if no sort specified)
        var sortBy = parameters.SortBy?.ToLower() ?? "email";
        var sortDescending = parameters.SortDescending ?? false;
        query = sortBy switch
        {
            "username" => sortDescending
                ? query.OrderByDescending(u => u.UserName)
                : query.OrderBy(u => u.UserName),
            _ => sortDescending
                ? query.OrderByDescending(u => u.Email)
                : query.OrderBy(u => u.Email)
        };

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply pagination
        var pageSize = Math.Min(parameters.PageSize, 100); // Max 100 items
        var page = Math.Max(parameters.Page, 1); // Min page 1
        var skip = (page - 1) * pageSize;

        var users = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        // Map to response DTOs
        var userResponses = new List<UserResponse>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userResponses.Add(new UserResponse
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                LockoutEnd = user.LockoutEnd,
                LockoutEnabled = user.LockoutEnabled,
                AccessFailedCount = user.AccessFailedCount,
                Roles = roles
            });
        }

        return PaginatedResult<UserResponse>.Create(userResponses, page, pageSize, totalCount);
    }

    /// <summary>
    /// Get user details by ID
    /// </summary>
    public async Task<ServiceResult<UserDetailResponse>> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult<UserDetailResponse>.Failure("User not found");
        }

        var roles = await _userManager.GetRolesAsync(user);

        var response = new UserDetailResponse
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LockoutEnd = user.LockoutEnd,
            LockoutEnabled = user.LockoutEnabled,
            AccessFailedCount = user.AccessFailedCount,
            Roles = roles
        };

        return ServiceResult<UserDetailResponse>.Success(response);
    }

    /// <summary>
    /// Create a new user (admin operation)
    /// </summary>
    public async Task<ServiceResult<UserResponse>> CreateUserAsync(CreateUserRequest request)
    {
        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            return ServiceResult<UserResponse>.Failure("A user with this email already exists");
        }

        // Check if username already exists (if provided)
        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            var existingByUsername = await _userManager.FindByNameAsync(request.UserName);
            if (existingByUsername is not null)
            {
                return ServiceResult<UserResponse>.Failure("A user with this username already exists");
            }
        }

        // Create user
        var user = new IdentityUser
        {
            UserName = request.UserName ?? request.Email,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = false
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors.Select(e => e.Description);
            _logger.LogError("Failed to create user {Email}: {Errors}",
                request.Email, string.Join(", ", errors));
            return ServiceResult<UserResponse>.Failure(errors);
        }

        // Assign roles (default to Customer if none specified)
        var rolesToAssign = request.Roles?.Any() == true ? request.Roles : new[] { Data.Roles.Customer };
        foreach (var role in rolesToAssign)
        {
            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning("Failed to assign role {Role} to user {UserId}",
                    role, user.Id);
            }
        }

        _logger.LogInformation("User created successfully: {UserId} - {Email}", user.Id, user.Email);

        var roles = await _userManager.GetRolesAsync(user);
        var response = new UserResponse
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            LockoutEnd = user.LockoutEnd,
            LockoutEnabled = user.LockoutEnabled,
            AccessFailedCount = user.AccessFailedCount,
            Roles = roles
        };

        return ServiceResult<UserResponse>.Success(response, "User created successfully");
    }

    /// <summary>
    /// Update user information (admin operation)
    /// </summary>
    public async Task<ServiceResult<UserResponse>> UpdateUserAsync(string userId, UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult<UserResponse>.Failure("User not found");
        }

        // Check if email is being changed and if it's already taken
        if (user.Email != request.Email)
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser is not null && existingUser.Id != userId)
            {
                return ServiceResult<UserResponse>.Failure("Email is already taken by another user");
            }
        }

        // Check if username is being changed and if it's already taken
        if (user.UserName != request.UserName)
        {
            var existingUser = await _userManager.FindByNameAsync(request.UserName);
            if (existingUser is not null && existingUser.Id != userId)
            {
                return ServiceResult<UserResponse>.Failure("Username is already taken by another user");
            }
        }

        // Update user properties
        user.Email = request.Email;
        user.UserName = request.UserName;
        user.PhoneNumber = request.PhoneNumber;
        user.EmailConfirmed = request.EmailConfirmed;
        user.LockoutEnabled = request.LockoutEnabled;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = updateResult.Errors.Select(e => e.Description);
            return ServiceResult<UserResponse>.Failure(errors);
        }

        _logger.LogInformation("User updated successfully: {UserId}", userId);

        var roles = await _userManager.GetRolesAsync(user);
        var response = new UserResponse
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            LockoutEnd = user.LockoutEnd,
            LockoutEnabled = user.LockoutEnabled,
            AccessFailedCount = user.AccessFailedCount,
            Roles = roles
        };

        return ServiceResult<UserResponse>.Success(response, "User updated successfully");
    }

    /// <summary>
    /// Delete user account (admin operation)
    /// </summary>
    public async Task<ServiceResult> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult.Failure("User not found");
        }

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            var errors = deleteResult.Errors.Select(e => e.Description);
            return ServiceResult.Failure(errors);
        }

        _logger.LogInformation("User deleted successfully: {UserId}", userId);

        return ServiceResult.Success("User deleted successfully");
    }

    /// <summary>
    /// Lock user account to prevent login
    /// </summary>
    public async Task<ServiceResult> LockUserAsync(string userId, DateTimeOffset? lockoutEnd)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult.Failure("User not found");
        }

        // If no lockout end specified, lock for 100 years (effectively permanent)
        var lockEnd = lockoutEnd ?? DateTimeOffset.UtcNow.AddYears(100);

        var lockResult = await _userManager.SetLockoutEndDateAsync(user, lockEnd);
        if (!lockResult.Succeeded)
        {
            var errors = lockResult.Errors.Select(e => e.Description);
            return ServiceResult.Failure(errors);
        }

        _logger.LogInformation("User locked successfully: {UserId} until {LockoutEnd}",
            userId, lockEnd);

        return ServiceResult.Success("User account locked successfully");
    }

    /// <summary>
    /// Unlock user account
    /// </summary>
    public async Task<ServiceResult> UnlockUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult.Failure("User not found");
        }

        // Clear lockout
        var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!unlockResult.Succeeded)
        {
            var errors = unlockResult.Errors.Select(e => e.Description);
            return ServiceResult.Failure(errors);
        }

        // Reset access failed count
        var resetResult = await _userManager.ResetAccessFailedCountAsync(user);
        if (!resetResult.Succeeded)
        {
            _logger.LogWarning("Failed to reset access failed count for user {UserId}", userId);
        }

        _logger.LogInformation("User unlocked successfully: {UserId}", userId);

        return ServiceResult.Success("User account unlocked successfully");
    }

    /// <summary>
    /// Get current user's full profile
    /// </summary>
    public async Task<ServiceResult<UserDetailResponse>> GetCurrentUserProfileAsync(string userId)
    {
        // Same as GetUserByIdAsync but for clarity in the API
        return await GetUserByIdAsync(userId);
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    public async Task<ServiceResult<UserResponse>> UpdateCurrentUserProfileAsync(
        string userId,
        UpdateProfileRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult<UserResponse>.Failure("User not found");
        }

        // Check if username is being changed and if it's already taken
        if (user.UserName != request.UserName)
        {
            var existingUser = await _userManager.FindByNameAsync(request.UserName);
            if (existingUser is not null && existingUser.Id != userId)
            {
                return ServiceResult<UserResponse>.Failure("Username is already taken");
            }
        }

        // Update user properties
        user.UserName = request.UserName;
        user.PhoneNumber = request.PhoneNumber;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = updateResult.Errors.Select(e => e.Description);
            return ServiceResult<UserResponse>.Failure(errors);
        }

        _logger.LogInformation("User profile updated: {UserId}", userId);

        var roles = await _userManager.GetRolesAsync(user);
        var response = new UserResponse
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            LockoutEnd = user.LockoutEnd,
            LockoutEnabled = user.LockoutEnabled,
            AccessFailedCount = user.AccessFailedCount,
            Roles = roles
        };

        return ServiceResult<UserResponse>.Success(response, "Profile updated successfully");
    }

    /// <summary>
    /// Change user's password
    /// </summary>
    public async Task<ServiceResult> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult.Failure("User not found");
        }

        var changeResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changeResult.Succeeded)
        {
            var errors = changeResult.Errors.Select(e => e.Description);
            _logger.LogWarning("Password change failed for user {UserId}: {Errors}",
                userId, string.Join(", ", errors));
            return ServiceResult.Failure(errors);
        }

        _logger.LogInformation("Password changed successfully for user: {UserId}", userId);

        return ServiceResult.Success("Password changed successfully");
    }

    /// <summary>
    /// Get user's roles
    /// </summary>
    public async Task<ServiceResult<IEnumerable<string>>> GetUserRolesAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult<IEnumerable<string>>.Failure("User not found");
        }

        var roles = await _userManager.GetRolesAsync(user);

        return ServiceResult<IEnumerable<string>>.Success(roles);
    }

    /// <summary>
    /// Assign role to user
    /// </summary>
    public async Task<ServiceResult> AddUserToRoleAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult.Failure("User not found");
        }

        // Check if role exists
        var roleExists = await _userManager.GetRolesAsync(user);
        if (roleExists.Contains(roleName))
        {
            return ServiceResult.Failure("User already has this role");
        }

        var addResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!addResult.Succeeded)
        {
            var errors = addResult.Errors.Select(e => e.Description);
            return ServiceResult.Failure(errors);
        }

        _logger.LogInformation("Role {Role} assigned to user {UserId}", roleName, userId);

        return ServiceResult.Success($"Role '{roleName}' assigned successfully");
    }

    /// <summary>
    /// Remove role from user
    /// </summary>
    public async Task<ServiceResult> RemoveUserFromRoleAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult.Failure("User not found");
        }

        // Check if user has the role
        var hasRole = await _userManager.IsInRoleAsync(user, roleName);
        if (!hasRole)
        {
            return ServiceResult.Failure("User does not have this role");
        }

        // Prevent removing last role
        var userRoles = await _userManager.GetRolesAsync(user);
        if (userRoles.Count <= 1)
        {
            return ServiceResult.Failure("Cannot remove the last role from user. Users must have at least one role.");
        }

        var removeResult = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (!removeResult.Succeeded)
        {
            var errors = removeResult.Errors.Select(e => e.Description);
            return ServiceResult.Failure(errors);
        }

        _logger.LogInformation("Role {Role} removed from user {UserId}", roleName, userId);

        return ServiceResult.Success($"Role '{roleName}' removed successfully");
    }
}
