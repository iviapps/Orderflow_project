using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using FluentValidation;
using Orderflow.Identity.Services.Roles;
using Orderflow.Identity.DTOs.Roles.Requests;
using Orderflow.Identity.Dtos.Common;
using Orderflow.Shared.Common;
using Orderflow.Identity.DTOs.Roles.Responses;

namespace Orderflow.Identity.Controllers; 

/// <summary>
/// Role management controller (Admin only)
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/admin/roles")]
[ApiVersion("1.0")]
[Tags("Role Management V1")]
[Authorize(Roles = "Admin")]
public class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly ILogger<RoleController> _logger;

    public RoleController(
        IRoleService roleService,
        ILogger<RoleController> logger)
    {
        _roleService = roleService;
        _logger = logger;
    }

    /// <summary>
    /// Get all roles
    /// </summary>
    /// <returns>List of all roles</returns>
    [HttpGet]
    [ProducesResponseType<IEnumerable<RoleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<RoleResponse>>> GetRoles()
    {
        _logger.LogInformation("Fetching all roles");

        var result = await _roleService.GetAllRolesAsync();

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to fetch roles: {Errors}", string.Join(", ", result.Errors));
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to fetch roles",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(result.Data!);
    }

    /// <summary>
    /// Get role by ID
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <returns>Role details</returns>
    [HttpGet("{roleId}")]
    [ProducesResponseType<RoleDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RoleDetailResponse>> GetRoleById(string roleId)
    {
        _logger.LogInformation("Fetching role: {RoleId}", roleId);

        var result = await _roleService.GetRoleByIdAsync(roleId);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Role not found: {RoleId}", roleId);
            return NotFound(new ProblemDetails
            {
                Title = "Role not found",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Data!);
    }

    /// <summary>
    /// Create a new role
    /// </summary>
    /// <param name="request">Role creation data</param>
    /// <param name="validator">Request validator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created role</returns>
    [HttpPost]
    [ProducesResponseType<RoleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RoleResponse>> CreateRole(
        [FromBody] CreateRoleRequest request,
        [FromServices] IValidator<CreateRoleRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            _logger.LogWarning("Role creation validation failed for: {RoleName}", request.RoleName);
            return ValidationProblem(new ValidationProblemDetails(errors)
            {
                Title = "Validation failed"
            });
        }

        _logger.LogInformation("Creating role: {RoleName}", request.RoleName);

        var result = await _roleService.CreateRoleAsync(request.RoleName);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Role creation failed for {RoleName}: {Errors}",
                request.RoleName, string.Join(", ", result.Errors));
            return BadRequest(new ProblemDetails
            {
                Title = "Failed to create role",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Role created successfully: {RoleName}", request.RoleName);

        return CreatedAtAction(nameof(GetRoleById), new { roleId = result.Data!.RoleId }, result.Data);
    }

    /// <summary>
    /// Update role
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <param name="request">Update data</param>
    /// <param name="validator">Request validator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated role</returns>
    [HttpPut("{roleId}")]
    [ProducesResponseType<RoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RoleResponse>> UpdateRole(
        string roleId,
        [FromBody] UpdateRoleRequest request,
        [FromServices] IValidator<UpdateRoleRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            _logger.LogWarning("Role update validation failed for role: {RoleId}", roleId);
            return ValidationProblem(new ValidationProblemDetails(errors)
            {
                Title = "Validation failed"
            });
        }

        _logger.LogInformation("Updating role: {RoleId} to {NewName}", roleId, request.RoleName);

        var result = await _roleService.UpdateRoleAsync(roleId, request.RoleName);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Role update failed for {RoleId}: {Errors}",
                roleId, string.Join(", ", result.Errors));

            if (result.Errors.Any(e => e.Contains("not found")))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Role not found",
                    Detail = string.Join(", ", result.Errors),
                    Status = StatusCodes.Status404NotFound
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Failed to update role",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Role updated successfully: {RoleId}", roleId);

        return Ok(result.Data!);
    }

    /// <summary>
    /// Delete role
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <returns>No content</returns>
    [HttpDelete("{roleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteRole(string roleId)
    {
        _logger.LogInformation("Deleting role: {RoleId}", roleId);

        var result = await _roleService.DeleteRoleAsync(roleId);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Role deletion failed for {RoleId}: {Errors}",
                roleId, string.Join(", ", result.Errors));

            if (result.Errors.Any(e => e.Contains("not found")))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Role not found",
                    Detail = string.Join(", ", result.Errors),
                    Status = StatusCodes.Status404NotFound
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Failed to delete role",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Role deleted successfully: {RoleId}", roleId);

        return NoContent();
    }

    /// <summary>
    /// Get users in a role
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <param name="pagination">Pagination parameters</param>
    /// <returns>Paginated list of users in the role</returns>
    [HttpGet("{roleId}/users")]
    [ProducesResponseType<PaginatedResponse<Orderflow.Identity.DTOs.Users.Responses.UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginatedResponse<Orderflow.Identity.DTOs.Users.Responses.UserResponse>>> GetRoleUsers(
        string roleId,
        [FromQuery] PaginationQuery pagination)
    {
        _logger.LogInformation("Fetching users for role: {RoleId}, Page: {Page}, PageSize: {PageSize}",
            roleId, pagination.Page, pagination.PageSize);

        var result = await _roleService.GetUsersInRoleAsync(roleId, pagination);

        // Check if role was not found (empty result with no total count)
        if (result.Pagination.TotalCount == 0 && !result.Data.Any())
        {
            _logger.LogWarning("Role not found or has no users: {RoleId}", roleId);
        }

        return Ok(result);
    }
}

