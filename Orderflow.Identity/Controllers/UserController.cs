using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using FluentValidation;
using System.Security.Claims;
using Orderflow.Identity.DTOs.Users.Responses;
using Orderflow.Identity.Services.Users;

namespace Orderflow.Identity.Controllers;

/// <summary>
/// User self-service controller for profile and password management
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/users")]
[ApiVersion("1.0")]
[Tags("User Self-Management V1")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IUserService userService,
        ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's profile
    /// </summary>
    /// <returns>Current user profile details</returns>
    [HttpGet("me")]
    [ProducesResponseType<Orderflow.Identity.DTOs.Users.Responses.UserDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Orderflow.Identity.DTOs.Users.Responses.UserDetailResponse>> GetMyProfile()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        _logger.LogInformation("Fetching profile for user: {UserId}", userId);

        var result = await _userService.GetCurrentUserProfileAsync(userId);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Profile not found for user: {UserId}", userId);
            return NotFound(new ProblemDetails
            {
                Title = "User profile not found",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Data!);
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    /// <param name="request">Profile update data</param>
    /// <param name="validator">Request validator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated user profile</returns>
    [HttpPut("me")]
    [ProducesResponseType<Orderflow.Identity.DTOs.Users.Responses.UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Orderflow.Identity.DTOs.Users.Responses.UserResponse>> UpdateMyProfile(
        [FromBody] Orderflow.Identity.DTOs.Users.Requests.UpdateProfileRequest request,
        [FromServices] IValidator<Orderflow.Identity.DTOs.Users.Requests.UpdateProfileRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            _logger.LogWarning("Profile update validation failed for: {UserId}", userId);
            return ValidationProblem(new ValidationProblemDetails(errors)
            {
                Title = "Validation failed"
            });
        }

        _logger.LogInformation("Updating profile for user: {UserId}", userId);

        var result = await _userService.UpdateCurrentUserProfileAsync(userId, request);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Profile update failed for {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors));

            return BadRequest(new ProblemDetails
            {
                Title = "Failed to update profile",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Profile updated successfully for user: {UserId}", userId);

        return Ok(result.Data!);
    }

    /// <summary>
    /// Change current user's password
    /// </summary>
    /// <param name="request">Password change data</param>
    /// <param name="validator">Request validator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    [HttpPost("me/password")]
    [ProducesResponseType<PasswordChangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PasswordChangeResponse>> ChangeMyPassword(
        [FromBody] Orderflow.Identity.DTOs.Users.Requests.ChangePasswordRequest request,
        [FromServices] IValidator<Orderflow.Identity.DTOs.Users.Requests.ChangePasswordRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            _logger.LogWarning("Password change validation failed for: {UserId}", userId);
            return ValidationProblem(new ValidationProblemDetails(errors)
            {
                Title = "Validation failed"
            });
        }

        _logger.LogInformation("Changing password for user: {UserId}", userId);

        var result = await _userService.ChangePasswordAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Password change failed for {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors));

            // Check if it's a wrong current password error
            if (result.Errors.Any(e => e.Contains("Incorrect password") || e.Contains("current password")))
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Current password is incorrect",
                    Detail = string.Join(", ", result.Errors),
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Failed to change password",
                Detail = string.Join(", ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Password changed successfully for user: {UserId}", userId);

        var response = new PasswordChangeResponse
        {
            Message = result.Message ?? "Password changed successfully"
        };

        return Ok(response);
    }
}
