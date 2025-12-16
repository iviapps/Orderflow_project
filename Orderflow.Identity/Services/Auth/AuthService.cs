using MassTransit;
using Microsoft.AspNetCore.Identity;
using Orderflow.Identity.DTOs.Auth;
using Orderflow.Identity.Services.Common;
using System.Linq;
using Orderflow.Shared.Events;
using Orderflow.ServiceDefaults;
using System.Diagnostics;

namespace Orderflow.Identity.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ITokenService tokenService,
            IPublishEndpoint publishEndpoint,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task<AuthResult<LoginResponse>> LoginAsync(LoginRequest request)
        {
            // ✅ EJEMPLO: Crear un span personalizado para rastrear el login
            using var activity = OrderflowActivitySource.StartActivity("User Login");
            activity?.SetTag("user.email", request.Email);

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                activity?.SetTag("login.result", "user_not_found");
                _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
                return AuthResult<LoginResponse>.Failure("Invalid email or password");
            }

            activity?.SetTag("user.id", user.Id);

            var signInResult = await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true);

            if (signInResult.IsLockedOut)
            {
                activity?.SetTag("login.result", "locked_out");
                _logger.LogWarning("User account locked out: {Email}", request.Email);
                return AuthResult<LoginResponse>.Failure(
                    "Account is locked due to multiple failed login attempts. Please try again later.");
            }

            if (!signInResult.Succeeded)
            {
                activity?.SetTag("login.result", "invalid_credentials");
                _logger.LogWarning("Failed login attempt for email: {Email}", request.Email);
                return AuthResult<LoginResponse>.Failure("Invalid email or password");
            }

            var roles = await _userManager.GetRolesAsync(user);
            activity?.SetTag("user.roles", string.Join(",", roles));

            var token = await _tokenService.GenerateAccessTokenAsync(user, roles);
            var expiresIn = _tokenService.GetTokenExpiryInSeconds();

            activity?.SetTag("login.result", "success");

            var response = new LoginResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = expiresIn,
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Roles = roles
            };

            return AuthResult<LoginResponse>.Success(response);
        }

        public async Task<AuthResult<RegisterResponse>> RegisterAsync(RegisterRequest request)
        {
            // ✅ EJEMPLO: Crear un span personalizado para rastrear el registro
            using var activity = OrderflowActivitySource.StartActivity("User Registration");
            activity?.SetTag("user.email", request.Email);

            try
            {
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser is not null)
                {
                    activity?.SetTag("registration.result", "email_exists");
                    _logger.LogWarning("Registration attempt with existing email: {Email}", request.Email);
                    return AuthResult<RegisterResponse>.Failure(
                        "A user with this email already exists");
                }

                //esta es la razón por la que el usuario se adjudica automaticamente.
                var userName = request.Email.Split('@')[0];

                var user = new IdentityUser
                {
                    UserName = userName,
                    Email = request.Email,
                    EmailConfirmed = false
                };

                // ✅ EJEMPLO: Registrar un evento en la traza
                OrderflowActivitySource.AddEvent("Creating user in database");

                var createResult = await _userManager.CreateAsync(user, request.Password);

                if (!createResult.Succeeded)
                {
                    var errors = createResult.Errors.Select(e => e.Description);
                    activity?.SetTag("registration.result", "creation_failed");
                    activity?.SetTag("registration.errors", string.Join(", ", errors));

                    _logger.LogError("Failed to create user {Email}: {Errors}",
                        request.Email, string.Join(", ", errors));

                    return AuthResult<RegisterResponse>.Failure(errors);
                }

                activity?.SetTag("user.id", user.Id);
                OrderflowActivitySource.AddEvent("User created successfully");

                var roleResult = await _userManager.AddToRoleAsync(user, Data.Roles.Customer);
                if (!roleResult.Succeeded)
                {
                    _logger.LogWarning("Failed to assign Customer role to user {UserId}: {Errors}",
                        user.Id, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
                else
                {
                    activity?.SetTag("user.role", Data.Roles.Customer);
                }

                //info injected.
                _logger.LogInformation("User successfully registered: {UserId} - {Email}",
                    user.Id, user.Email);

                // ✅ EJEMPLO: El evento MassTransit automáticamente propagará el contexto de traza
                OrderflowActivitySource.AddEvent("Publishing UserRegistered event");
                var userRegisteredEvent = new UserRegisteredEvent(
                    UserId: user.Id,
                    Email: user.Email!,
                    FirstName: null,
                    LastName: null);

                await _publishEndpoint.Publish(userRegisteredEvent);

                activity?.SetTag("registration.result", "success");

                var response = new RegisterResponse
                {
                    UserId = user.Id,
                    Email = user.Email ?? string.Empty,
                    Message = "User registered successfully. Please check your email to confirm your account."
                };

                return AuthResult<RegisterResponse>.Success(response);
            }
            catch (Exception ex)
            {
                // ✅ EJEMPLO: Registrar excepciones en la traza
                OrderflowActivitySource.RecordException(ex);
                throw;
            }
        }

        public async Task<AuthResult<CurrentUserResponse>> GetCurrentUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                _logger.LogWarning("Get current user failed: User not found {UserId}", userId);
                return AuthResult<CurrentUserResponse>.Failure("User not found");
            }

            var roles = await _userManager.GetRolesAsync(user);

            var response = new CurrentUserResponse
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Roles = roles
            };

            return AuthResult<CurrentUserResponse>.Success(response);
        }
    }
}
