using Microsoft.AspNetCore.Identity;          // API de Identity (UserManager, SignInManager, IdentityUser, etc.)
using Microsoft.AspNetCore.Mvc;              // Atributos y tipos de ASP.NET Core MVC / Web API (ControllerBase, Route, etc.)
using FluentValidation;                      // FluentValidation para validar DTOs de entrada
using Microsoft.IdentityModel.Tokens;        // Tipos para firmar y validar tokens JWT
using System.IdentityModel.Tokens.Jwt;       // Construcción y escritura de JWT
using System.Security.Claims;               // Claims (NameIdentifier, Role, etc.)
using System.Text;                           // Encoding para convertir el secret a bytes
using Microsoft.AspNetCore.Authorization;    // Atributos [Authorize], [AllowAnonymous]

namespace OrderFlow.Identity.Controllers.V2;  // Namespace lógico de la capa de API (v2 de Identity)

[ApiController]                               // Marca el controller como API (binding automático, validación de modelo, etc.)
[Route("api/v{version:apiVersion}/auth")]     // Ruta base: api/v2/auth por ejemplo
[ApiVersion("2.0")]                           // Versión de la API gestionada por Asp.Versioning (v2)
[Tags("Authentication V2")]                   // Tag para documentación (Swagger/OpenAPI), agrupa endpoints bajo "Authentication V2"
public class AuthController : ControllerBase  // Controller sin vistas, solo API
{
    // Dependencias inyectadas vía constructor (DI container)
    private readonly UserManager<IdentityUser> _userManager;           // Gestión de usuarios (crear, buscar, roles, etc.)
    private readonly SignInManager<IdentityUser> _signInManager;       // Lógica de login / sign-in (validar password, lockout, etc.)
    private readonly IConfiguration _configuration;                    // Acceso a configuración (appsettings, secretos, etc.)
    private readonly ILogger<AuthController> _logger;                  // Logging estructurado para este controller

    public AuthController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        // Asignación de dependencias a campos privados
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Login user
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="validator">Request validator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT token and user information</returns>
    [HttpPost("login")]                                           // POST api/v2/auth/login
    [AllowAnonymous]                                              // No requiere estar autenticado
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)] // Documenta que devuelve 200 con LoginResponse
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)] // Documenta 400 con ErrorResponse
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)] // Documenta 401 con ErrorResponse
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,                          // DTO de entrada del body (email y password)
        [FromServices] IValidator<LoginRequest> validator,         // Validator de FluentValidation inyectado desde DI
        CancellationToken cancellationToken = default)             // Permite cancelar la operación si el cliente se desconecta
    {
        // 1) Validación del request con FluentValidation
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            // Proyectamos los mensajes de error
            var errors = validationResult.Errors.Select(e => e.ErrorMessage);
            // Log a nivel Warning con info contextual (email)
            _logger.LogWarning("Login validation failed for email: {Email}", request.Email);
            // Retornamos 400 con la lista de errores
            return BadRequest(new ErrorResponse(errors));
        }

        // 2) Buscamos el usuario por email en la base de datos de Identity
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Log de intento de login con un email inexistente
            _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
            // Por seguridad, devolvemos mensaje genérico (no decimos si el email existe o no)
            return BadRequest(new ErrorResponse(["Invalid email or password"]));
        }

        // 3) Validamos la contraseña y gestionamos lockouts
        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true); // Si falla varias veces, bloquea la cuenta según política de lockout

        if (result.IsLockedOut)
        {
            // Si el usuario está bloqueado, devolvemos mensaje específico
            _logger.LogWarning("User account locked out: {Email}", request.Email);
            return BadRequest(new ErrorResponse([
                "Account is locked due to multiple failed login attempts. Please try again later."
            ]));
        }

        if (!result.Succeeded)
        {
            // Login incorrecto (password incorrecta, etc.)
            _logger.LogWarning("Failed login attempt for email: {Email}", request.Email);
            return BadRequest(new ErrorResponse(["Invalid email or password"]));
        }

        // 4) Obtenemos los roles del usuario desde Identity
        var roles = await _userManager.GetRolesAsync(user);

        // 5) Generamos el token JWT para este usuario con sus claims y roles
        var token = GenerateJwtToken(user, roles);

        // Leemos el tiempo de expiración desde configuración (con fallback a 60 minutos)
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryInMinutes"] ?? "60");

        // Log de login exitoso con info contextual
        _logger.LogInformation("User successfully logged in: {UserId} - {Email}", user.Id, user.Email);

        // 6) Montamos el DTO de respuesta
        var response = new LoginResponse
        {
            AccessToken = token,            // JWT generado
            TokenType = "Bearer",           // Convención estándar para Authorization: Bearer {token}
            ExpiresIn = expiryMinutes * 60, // En segundos (útil para front)
            UserId = user.Id,
            Email = user.Email!,
            Roles = roles                   // Roles asociados a este usuario
        };

        // Devolvemos 200 OK con el token y datos del usuario
        return Ok(response);
    }
    /// <summary>
    /// Register new user
    /// </summary>
    /// <param name="request">Registration data</param>
    /// <param name="validator">Request validator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Registration confirmation</returns>
    [HttpPost("register")]                                        // POST api/v2/auth/register
    [AllowAnonymous]                                              // Público, no requiere autenticación
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)] // 201 Created con info del usuario
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)] // 400 con errores de validación o conflicto
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,                       // DTO con email / password / confirmPassword
        [FromServices] IValidator<RegisterRequest> validator,     // Validator específico para registro
        CancellationToken cancellationToken = default)
    {
        // 1) Validamos el request de registro
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage);
            _logger.LogWarning("Registration validation failed for email: {Email}", request.Email);
            return BadRequest(new ErrorResponse(errors));
        }

        // 2) Comprobamos si ya existe un usuario con ese email
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", request.Email);
            return BadRequest(new ErrorResponse(["User with this email already exists"]));
        }

        // 3) Creamos la entidad IdentityUser a partir del request
        var user = new IdentityUser
        {
            UserName = request.Email,       // Usamos el email como nombre de usuario
            Email = request.Email,
            EmailConfirmed = true           // En producción normalmente sería false y habría proceso de confirmación
        };

        // 4) Creamos el usuario en la base de datos de Identity con su password
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            // Si falla la creación, sacamos las descripciones de los errores de Identity
            var errors = result.Errors.Select(e => e.Description);
            _logger.LogWarning("User creation failed for email: {Email}. Errors: {@Errors}",
                request.Email, errors);
            return BadRequest(new ErrorResponse(errors));
        }

        // 5) Asignamos un rol por defecto, por ejemplo "Customer"
        await _userManager.AddToRoleAsync(user, "Customer");

        _logger.LogInformation("User successfully registered: {UserId} - {Email}", user.Id, user.Email);

        // 6) Armamos la respuesta de registro
        var response = new RegisterResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Message = "User registered successfully"
        };

        // Devolvemos 201 Created, apuntando lógicamente al endpoint de Login (aunque no se usa la ruta)
        return CreatedAtAction(nameof(Login), new { }, response);
    }
    /// <summary>
    /// Get current user information
    /// </summary>
    /// <returns>Current user information</returns>
    [HttpGet("me")]                                               // GET api/v2/auth/me
    [Authorize]                                                   // Requiere un JWT válido
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser()
    {
        // 1) Extraemos el userId de los claims del token JWT
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            // Si no hay claim, el token es inválido o mal emitido
            return Unauthorized();
        }

        // 2) Buscamos el usuario en la base de datos de Identity
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            // Si no existe, algo está inconsistente (token emitido para usuario inexistente)
            return Unauthorized();
        }

        // 3) Obtenemos roles del usuario
        var roles = await _userManager.GetRolesAsync(user);

        // 4) Montamos la respuesta con la info básica del usuario y sus roles
        var response = new CurrentUserResponse
        {
            UserId = user.Id,
            Email = user.Email!,
            Roles = roles
        };

        return Ok(response);
    }

    /// <summary>
    /// Admin only endpoint
    /// </summary>
    /// <returns>Admin confirmation message</returns>
    [HttpGet("admin-only")]                                      // GET api/v2/auth/admin-only
    [ProducesResponseType<AdminOnlyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin")]                                 // Solo accesible si el usuario tiene rol "Admin"
    public ActionResult<AdminOnlyResponse> AdminOnly()
    {
        // Respuesta simple para validar que el pipeline de roles funciona
        var response = new AdminOnlyResponse
        {
            Message = "You are an admin!",
            Timestamp = DateTime.UtcNow
        };

        return Ok(response);
    }

    //generamos token para el usuario con sus roles 
    private string GenerateJwtToken(IdentityUser user, IList<string> roles)
    {
        // 1) Leemos la configuración de JWT desde appsettings / secretos
        var jwtSecret = _configuration["Jwt:Secret"]!;         // Clave simétrica (no se debe exponer)
        var jwtIssuer = _configuration["Jwt:Issuer"]!;         // Emisor del token
        var jwtAudience = _configuration["Jwt:Audience"]!;     // Audiencia (quién puede consumir el token)
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryInMinutes"] ?? "60"); // Tiempo de vida

        // 2) Creamos la clave simétrica a partir del secret
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        // 3) Configuramos las credenciales de firmado usando HMAC-SHA256
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // 4) Definimos los claims principales del token
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),                  // Subject (ID de usuario)
            new(JwtRegisteredClaimNames.Email, user.Email!),            // Email
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),// ID único de token (para revocación, tracking)
            new(ClaimTypes.NameIdentifier, user.Id)                     // Claim estándar de .NET para NameIdentifier
        };

        // 5) Añadimos los roles como claims de tipo Role
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // 6) Construimos el token JWT
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        // 7) Escribimos el token en formato string (compact serialization)
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// DTO que recibe el endpoint de login
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;          // Email del usuario
    public string Password { get; set; } = string.Empty;       // Password en texto plano (se encripta en el servidor)
}

// DTO que devuelve el endpoint de login
public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;    // JWT emitido
    public string TokenType { get; set; } = string.Empty;      // Normalmente "Bearer"
    public int ExpiresIn { get; set; }                         // Segundos hasta la expiración
    public string UserId { get; set; } = string.Empty;         // Id del usuario en Identity
    public string Email { get; set; } = string.Empty;          // Email del usuario
    public IEnumerable<string> Roles { get; set; } = [];       // Roles asociados
}

// DTO para el registro
public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;          // Email que se registrará
    public string Password { get; set; } = string.Empty;       // Password
    public string ConfirmPassword { get; set; } = string.Empty;// Confirmación (validada vía FluentValidation)
}

// Respuesta al registrarse
public class RegisterResponse
{
    public string UserId { get; set; } = string.Empty;         // Id del usuario creado
    public string Email { get; set; } = string.Empty;          // Email del usuario
    public string Message { get; set; } = string.Empty;        // Mensaje de confirmación
}

// Respuesta para el endpoint /me
public class CurrentUserResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IEnumerable<string> Roles { get; set; } = [];       // Roles del usuario actual
}

// DTO estándar para devolver errores en formato consistente
public class ErrorResponse
{
    public IEnumerable<string> Errors { get; set; }            // Lista de mensajes de error

    public ErrorResponse(IEnumerable<string> errors)
    {
        Errors = errors;
    }
}

// Respuesta del endpoint exclusivo para Admin
public class AdminOnlyResponse
{
    public string Message { get; set; } = string.Empty;        // Mensaje de confirmación
    public DateTime Timestamp { get; set; }                    // Marca temporal del servidor
}
