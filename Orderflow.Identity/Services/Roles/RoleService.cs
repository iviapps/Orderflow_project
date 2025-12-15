using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orderflow.Identity.DTOs.Roles.Responses;
using Orderflow.Identity.DTOs.Users.Responses;
using Orderflow.Shared.Common;


namespace Orderflow.Identity.Services.Roles;

/// <summary>
/// Service for role management operations
/// </summary>
public class RoleService : IRoleService
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        RoleManager<IdentityRole> roleManager,
        UserManager<IdentityUser> userManager,
        ILogger<RoleService> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Get all roles
    /// </summary>
    public async Task<ServiceResult<IEnumerable<RoleResponse>>> GetAllRolesAsync()
    {
        var roles = await _roleManager.Roles.ToListAsync();

        var roleResponses = new List<RoleResponse>();
        foreach (var role in roles)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
            roleResponses.Add(new RoleResponse
            {
                RoleId = role.Id,
                RoleName = role.Name ?? string.Empty,
                NormalizedName = role.NormalizedName ?? string.Empty,
                UserCount = usersInRole.Count
            });
        }

        return ServiceResult<IEnumerable<RoleResponse>>.Success(roleResponses);
    }

    /// <summary>
    /// Get role details by ID
    /// </summary>
    public async Task<ServiceResult<RoleDetailResponse>> GetRoleByIdAsync(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            return ServiceResult<RoleDetailResponse>.Failure("Role not found");
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);

        var response = new RoleDetailResponse
        {
            RoleId = role.Id,
            RoleName = role.Name ?? string.Empty,
            NormalizedName = role.NormalizedName ?? string.Empty,
            UserCount = usersInRole.Count
        };

        return ServiceResult<RoleDetailResponse>.Success(response);
    }

    /// <summary>
    /// Create a new role
    /// </summary>
    public async Task<ServiceResult<RoleResponse>> CreateRoleAsync(string roleName)
    {
        // Check if role already exists
        var existingRole = await _roleManager.FindByNameAsync(roleName);
        if (existingRole is not null)
        {
            return ServiceResult<RoleResponse>.Failure("A role with this name already exists");
        }

        var role = new IdentityRole(roleName);
        var createResult = await _roleManager.CreateAsync(role);

        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors.Select(e => e.Description);
            _logger.LogError("Failed to create role {RoleName}: {Errors}",
                roleName, string.Join(", ", errors));
            return ServiceResult<RoleResponse>.Failure(errors);
        }

        _logger.LogInformation("Role created successfully: {RoleId} - {RoleName}", role.Id, role.Name);

        var response = new RoleResponse
        {
            RoleId = role.Id,
            RoleName = role.Name ?? string.Empty,
            NormalizedName = role.NormalizedName ?? string.Empty,
            UserCount = 0
        };

        return ServiceResult<RoleResponse>.Success(response, "Role created successfully");
    }

    /// <summary>
    /// Update role name
    /// </summary>
    public async Task<ServiceResult<RoleResponse>> UpdateRoleAsync(string roleId, string newRoleName)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            return ServiceResult<RoleResponse>.Failure("Role not found");
        }

        // Check if new name is already taken by another role
        var existingRole = await _roleManager.FindByNameAsync(newRoleName);
        if (existingRole is not null && existingRole.Id != roleId)
        {
            return ServiceResult<RoleResponse>.Failure("A role with this name already exists");
        }

        role.Name = newRoleName;
        var updateResult = await _roleManager.UpdateAsync(role);

        if (!updateResult.Succeeded)
        {
            var errors = updateResult.Errors.Select(e => e.Description);
            return ServiceResult<RoleResponse>.Failure(errors);
        }

        _logger.LogInformation("Role updated successfully: {RoleId} - {RoleName}", role.Id, role.Name);

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        var response = new RoleResponse
        {
            RoleId = role.Id,
            RoleName = role.Name ?? string.Empty,
            NormalizedName = role.NormalizedName ?? string.Empty,
            UserCount = usersInRole.Count
        };

        return ServiceResult<RoleResponse>.Success(response, "Role updated successfully");
    }

    /// <summary>
    /// Delete role
    /// </summary>
    public async Task<ServiceResult> DeleteRoleAsync(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            return ServiceResult.Failure("Role not found");
        }

        // Check if role has users
        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Any())
        {
            return ServiceResult.Failure(
                $"Cannot delete role '{role.Name}' because it has {usersInRole.Count} user(s) assigned to it. Remove all users from this role first.");
        }

        var deleteResult = await _roleManager.DeleteAsync(role);
        if (!deleteResult.Succeeded)
        {
            var errors = deleteResult.Errors.Select(e => e.Description);
            return ServiceResult.Failure(errors);
        }

        _logger.LogInformation("Role deleted successfully: {RoleId} - {RoleName}", roleId, role.Name);

        return ServiceResult.Success("Role deleted successfully");
    }

    /// <summary>
    /// Get users in a specific role (paginated)
    /// </summary>
    public async Task<PaginatedResult<UserResponse>> GetUsersInRoleAsync(
        string roleId,
        PaginationQuery pagination)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            return new PaginatedResult<UserResponse>
            {
                Data = new List<UserResponse>(),
                Pagination = new PaginationMetadata
                {
                    Page = pagination.Page,
                    PageSize = pagination.PageSize,
                    TotalCount = 0,
                    TotalPages = 0
                }
            };
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        var totalCount = usersInRole.Count;

        // Apply pagination
        var pageSize = Math.Min(pagination.PageSize, 100);
        var page = Math.Max(pagination.Page, 1);
        var skip = (page - 1) * pageSize;

        var paginatedUsers = usersInRole
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        // Map to response DTOs
        var userResponses = new List<UserResponse>();
        foreach (var user in paginatedUsers)
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
}
