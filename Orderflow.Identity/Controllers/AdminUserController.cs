using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using FluentValidation;
using Orderflow.Identity.DTOs.Users.Requests;
using Orderflow.Identity.DTOs.Users.Responses;
using Orderflow.Identity.Services.Users;
using Orderflow.Identity.Dtos.Common;

namespace OrderFlow.Identity.Controllers; 

/// <summary>
/// Admin user management controller
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/admin/users")]
[ApiVersion("1.0")]
[Tags("User Management V1")]
[Authorize(Roles = "Admin")]
public class AdminUserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<AdminUserController> _logger;

    public AdminUserController(
        IUserService userService,
        ILogger<AdminUserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users with pagination and filtering
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination</param>
    /// <returns>Paginated list of users</returns>
    [HttpGet]
    [ProducesResponseType<PaginatedResponse<Orderflow.Identity.DTOs.Users.Responses.UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginatedResponse<Orderflow.Identity.DTOs.Users.Responses.UserResponse>>> GetUsers(
        [FromQuery] Orderflow.Identity.DTOs.Users.Queries.UserQueryParameters query)
    {
        _logger.LogInformation("Fetching users: Page {Page}, PageSize {PageSize}, Search {Search}, Role {Role}",
            query.Page, query.PageSize, query.Search ?? "none", query.Role ?? "none");

        var result = await _userService.GetUsersAsync(query);

        return Ok(result);
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User details</returns>
    [HttpGet("{userId}")]
    [ProducesResponseType<Orderflow.Identity.DTOs.Users.Responses.UserDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Orderflow.Identity.DTOs.Users.Responses.UserDetailResponse>> GetUserById(string userId)
    {
        _logger.LogInformation("Fetching user: {UserId}", userId);

        var result = await _userService.GetUserByIdAsync(userId);

        if (!result.Succeeded)
        {
            _logger.LogWarning("User not found: {UserId}", userId);
            return NotFound(new ProblemDetails
            {
                Title = "User not found",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Data!);
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    /// <param name="request">User creation data</param>
    /// <param name="validator">Request validator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created user</returns>
    [HttpPost]
    [ProducesResponseType<Orderflow.Identity.DTOs.Users.Responses.UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Orderflow.Identity.DTOs.Users.Responses.UserResponse>> CreateUser(
        [FromBody] Orderflow.Identity.DTOs.Users.Requests.CreateUserRequest request,
        [FromServices] IValidator<Orderflow.Identity.DTOs.Users.Requests.CreateUserRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            _logger.LogWarning("User creation validation failed");
            return ValidationProblem(new ValidationProblemDetails(errors)
            {
                Title = "Validation failed"
            });
        }

        var result = await _userService.CreateUserAsync(request);

        if (!result.Succeeded)
        {
            _logger.LogWarning("User creation failed: {Errors}", string.Join(", ", result.Errors));
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to create user",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("User created successfully: {UserId}", result.Data!.UserId);

        return CreatedAtAction(nameof(GetUserById), new { userId = result.Data.UserId }, result.Data);
    }

    /// <summary>
    /// Update user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="request">Update data</param>
    /// <param name="validator">Request validator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated user</returns>
    [HttpPut("{userId}")]
    [ProducesResponseType<Orderflow.Identity.DTOs.Users.Responses.UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Orderflow.Identity.DTOs.Users.Responses.UserResponse>> UpdateUser(
        string userId,
        [FromBody] Orderflow.Identity.DTOs.Users.Requests.UpdateUserRequest request,
        [FromServices] IValidator<Orderflow.Identity.DTOs.Users.Requests.UpdateUserRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            _logger.LogWarning("User update validation failed for user: {UserId}", userId);
            return ValidationProblem(new ValidationProblemDetails(errors)
            {
                Title = "Validation failed"
            });
        }

        var result = await _userService.UpdateUserAsync(userId, request);

        if (!result.Succeeded)
        {
            _logger.LogWarning("User update failed for {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors));

            if (result.Errors.Any(e => e.Contains("not found")))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User not found",
                    Detail = string.Join(", ", result.Errors),
                    Status = StatusCodes.Status404NotFound
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Failed to update user",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("User updated successfully: {UserId}", userId);

        return Ok(result.Data!);
    }

    /// <summary>
    /// Delete user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>No content</returns>
    [HttpDelete("{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        _logger.LogInformation("Deleting user: {UserId}", userId);

        var result = await _userService.DeleteUserAsync(userId);

        if (!result.Succeeded)
        {
            _logger.LogWarning("User deletion failed for {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors));

            if (result.Errors.Any(e => e.Contains("not found")))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User not found",
                    Detail = string.Join(", ", result.Errors),
                    Status = StatusCodes.Status404NotFound
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Failed to delete user",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("User deleted successfully: {UserId}", userId);

        return NoContent();
    }

    /// <summary>
    /// Lock user account
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="request">Lock request with optional lockout end time. If not provided or null, locks indefinitely.</param>
    /// <returns>No content</returns>
    [HttpPost("{userId}/lock")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LockUser(
        string userId,
        [FromBody] LockUserRequest? request = null)
    {
        var lockoutEnd = request?.LockoutEnd ?? DateTimeOffset.MaxValue;

        _logger.LogInformation("Locking user: {UserId} until {LockoutEnd}", userId, lockoutEnd);

        var result = await _userService.LockUserAsync(userId, lockoutEnd);

        if (!result.Succeeded)
        {
            _logger.LogWarning("User lock failed for {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors));

            if (result.Errors.Any(e => e.Contains("not found")))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User not found",
                    Detail = string.Join(", ", result.Errors),
                    Status = StatusCodes.Status404NotFound
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Failed to lock user",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("User locked successfully: {UserId}", userId);

        return NoContent();
    }

    /// <summary>
    /// Unlock user account
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>No content</returns>
    [HttpPost("{userId}/unlock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UnlockUser(string userId)
    {
        _logger.LogInformation("Unlocking user: {UserId}", userId);

        var result = await _userService.UnlockUserAsync(userId);

        if (!result.Succeeded)
        {
            _logger.LogWarning("User unlock failed for {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors));

            if (result.Errors.Any(e => e.Contains("not found")))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User not found",
                    Detail = string.Join(", ", result.Errors),
                    Status = StatusCodes.Status404NotFound
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Failed to unlock user",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("User unlocked successfully: {UserId}", userId);

        return NoContent();
    }

    /// <summary>
    /// Get user's roles
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of role names</returns>
    [HttpGet("{userId}/roles")]
    [ProducesResponseType<UserRolesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserRolesResponse>> GetUserRoles(string userId)
    {
        _logger.LogInformation("Fetching roles for user: {UserId}", userId);

        var result = await _userService.GetUserRolesAsync(userId);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to get roles for user {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors));

            if (result.Errors.Any(e => e.Contains("not found")))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User not found",
                    Detail = string.Join(", ", result.Errors),
                    Status = StatusCodes.Status404NotFound
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Failed to get user roles",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        var response = new UserRolesResponse
        {
            UserId = userId,
            Roles = result.Data!
        };

        return Ok(response);
    }

    /// <summary>
    /// Assign role to user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="roleName">Role name to assign</param>
    /// <returns>Success message</returns>
    [HttpPost("{userId}/roles/{roleName}")]
    [ProducesResponseType<RoleAssignmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RoleAssignmentResponse>> AssignUserRole(
        string userId,
        string roleName)
    {
        _logger.LogInformation("Assigning role {Role} to user: {UserId}", roleName, userId);

        var result = await _userService.AddUserToRoleAsync(userId, roleName);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Role assignment failed for {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors));

            if (result.Errors.Any(e => e.Contains("not found")))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User or role not found",
                    Detail = string.Join(", ", result.Errors),
                    Status = StatusCodes.Status404NotFound
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Failed to assign role",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Role {Role} assigned to user {UserId} successfully", roleName, userId);

        var response = new RoleAssignmentResponse
        {
            UserId = userId,
            RoleName = roleName,
            Message = result.Message ?? "Role assigned successfully"
        };

        return Ok(response);
    }

    /// <summary>
    /// Remove role from user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="roleName">Role name to remove</param>
    /// <returns>No content</returns>
    [HttpDelete("{userId}/roles/{roleName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveUserRole(
        string userId,
        string roleName)
    {
        _logger.LogInformation("Removing role {Role} from user: {UserId}", roleName, userId);

        var result = await _userService.RemoveUserFromRoleAsync(userId, roleName);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Role removal failed for {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors));

            if (result.Errors.Any(e => e.Contains("not found")))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User or role not found",
                    Detail = string.Join(", ", result.Errors),
                    Status = StatusCodes.Status404NotFound
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Failed to remove role",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Role {Role} removed from user {UserId} successfully", roleName, userId);

        return NoContent();
    }
}

