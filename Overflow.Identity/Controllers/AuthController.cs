using Microsoft.AspNetCore.Identity;          // UserManager, SignInManager, IdentityUser, etc.
using Microsoft.AspNetCore.Mvc;              // ControllerBase, [ApiController], [Route], etc.
using FluentValidation;                      // IValidator<T> para validar DTOs
using Microsoft.IdentityModel.Tokens;        // Firmar y validar tokens JWT
using System.IdentityModel.Tokens.Jwt;       // Construcción / escritura de JWT
using System.Security.Claims;                // Claims
using System.Text;                           // Encoding.UTF8
using Microsoft.AspNetCore.Authorization;    // [Authorize], [AllowAnonymous]

namespace OrderFlow.Identity.Controllers      // Namespace normal, sin ".V2"
{
    [ApiController]                               // Controller de API
    [Route("api/auth")]                           // Ruta base: /api/auth
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(

     UserManager<IdentityUser> userManager,       // Motor principal para gestionar usuarios: a BBDD
                                                  // - Crear usuarios (CreateAsync)
                                                  // - Buscar por email/ID (FindByEmailAsync, FindByIdAsync)
                                                  // - Cambiar password (ChangePasswordAsync)
                                                  // - Comprobar password (CheckPasswordAsync)
                                                  // - Gestionar roles, claims, bloqueos, etc.
                                                  // Es la capa de acceso de alto nivel a la tabla AspNetUsers.

    SignInManager<IdentityUser> signInManager,   // Gestor del proceso de inicio de sesión:
                                                 // - Validar credenciales (PasswordSignInAsync)
                                                 // - Comprobar bloqueos y 2FA
                                                 // - Manejar cookies de autenticación si las hubiera
                                                 // En APIs, lo usas para validar el login antes de emitir un JWT.

    IConfiguration configuration,                 // Acceso a appsettings.json:
                                                  // - Leer la clave secreta del JWT (Jwt:Key)
                                                  // - Leer issuer/audience (Jwt:Issuer, Jwt:Audience)
                                                  // - Config de expiración de tokens
                                                  // - Cualquier otro valor de la configuración
                                                  // Es la fuente de settings para generar el JWT.

    ILogger<AuthController> logger               // Registro de auditoría y diagnóstico:
                                                 // - Login fallido → logger.LogWarning(...)
                                                 // - Usuario no encontrado → logger.LogWarning(...)
                                                 // - Errores internos → logger.LogError(...)
                                                 // Te permite trazar eventos de seguridad y depurar incidentes.
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Login user.
        /// </summary>
        [HttpPost("login")]   // POST /api/auth/login
        [AllowAnonymous]
        [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<LoginResponse>> Login(
            [FromBody] LoginRequest request,
            [FromServices] IValidator<LoginRequest> validator,
            CancellationToken cancellationToken = default)
        {
            // 1) Validación con FluentValidation
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage);
                _logger.LogWarning("Login validation failed for email: {Email}", request.Email);
                return BadRequest(new ErrorResponse(errors));
            }

            // 2) Buscar usuario por email
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
                return BadRequest(new ErrorResponse(["Invalid email or password"]));
            }

            // 3) Validar contraseña y gestionar lockout
            var result = await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out: {Email}", request.Email);
                return BadRequest(new ErrorResponse([
                    "Account is locked due to multiple failed login attempts. Please try again later."
                ]));
            }

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed login attempt for email: {Email}", request.Email);
                return BadRequest(new ErrorResponse(["Invalid email or password"]));
            }

            // 4) Obtener roles
            var roles = await _userManager.GetRolesAsync(user);

            // 5) Generar JWT <- 
            //EL TOKEN SE GENERA EN EL LOGIN GRACIAS A ESTA LINEA 
            var token = GenerateJwtToken(user, roles);
            var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryInMinutes"] ?? "60");

            _logger.LogInformation("User successfully logged in: {UserId} - {Email}", user.Id, user.Email);

            // 6) DTO de respuesta
            var response = new LoginResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = expiryMinutes * 60,
                UserId = user.Id,
                Email = user.Email!,
                Roles = roles
            };

            return Ok(response);
        }

        /// <summary>
        /// Register new user.
        /// </summary>
        [HttpPost("register")]    // POST /api/auth/register
        [AllowAnonymous]
        [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<RegisterResponse>> Register(
            [FromBody] RegisterRequest request,
            [FromServices] IValidator<RegisterRequest> validator,
            CancellationToken cancellationToken = default)
        {
            // 1) Validación
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage);
                _logger.LogWarning("Registration validation failed for email: {Email}", request.Email);
                return BadRequest(new ErrorResponse(errors));
            }

            // 2) Comprobar si ya existe
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration attempt with existing email: {Email}", request.Email);
                return BadRequest(new ErrorResponse(["User with this email already exists"]));
            }

            // 3) Crear IdentityUser
            var user = new IdentityUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true
            };

            // 4) Crear en BBDD
            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                _logger.LogWarning("User creation failed for email: {Email}. Errors: {@Errors}", request.Email, errors);
                return BadRequest(new ErrorResponse(errors));
            }

            // 5) Asignar rol por defecto
            await _userManager.AddToRoleAsync(user, "Customer");

            _logger.LogInformation("User successfully registered: {UserId} - {Email}", user.Id, user.Email);

            // 6) Respuesta
            var response = new RegisterResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Message = "User registered successfully"
            };

            return CreatedAtAction(nameof(Login), new { }, response);
        }

        /// <summary>
        /// Get current user information.
        /// </summary>
        [HttpGet("me")]   // GET /api/auth/me
        [Authorize]
        [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser()
        {
            // 1) Obtener userId del token
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // 2) Buscar usuario
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized();

            // 3) Roles
            var roles = await _userManager.GetRolesAsync(user);

            // 4) Respuesta
            var response = new CurrentUserResponse
            {
                UserId = user.Id,
                Email = user.Email!,
                Roles = roles
            };

            return Ok(response);
        }

        // --- JWT helper ---
        private string GenerateJwtToken(IdentityUser user, IList<string> roles)
        {
            var jwtSecret = _configuration["Jwt:Secret"]!;
            var jwtIssuer = _configuration["Jwt:Issuer"]!;
            var jwtAudience = _configuration["Jwt:Audience"]!;
            var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryInMinutes"] ?? "60");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Email, user.Email!),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, user.Id)
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // DTOs / Models

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public IEnumerable<string> Roles { get; set; } = Array.Empty<string>();
    }

    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class RegisterResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class CurrentUserResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public IEnumerable<string> Roles { get; set; } = Array.Empty<string>();
    }

    public class ErrorResponse
    {
        public IEnumerable<string> Errors { get; set; }

        public ErrorResponse(IEnumerable<string> errors)
        {
            Errors = errors;
        }
    }

    public class AdminOnlyResponse
    {
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
