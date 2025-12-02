using Asp.Versioning;
using Orderflow.Identity.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;
using Orderflow.Identity.Services;

namespace Orderflow.Identity.Controllers
{

    
    [ApiController]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Llamamos al servicio de autenticación
            var result = await _authService.LoginAsync(request);

            // OJO: la propiedad se llama Succeeded, no Success
            if (!result.Succeeded)
                // Puedes devolver directamente los errores o envolverlos en tu ErrorResponse
                return Unauthorized(result.Errors);

            // Si todo va bien, devolvemos el payload (Data) o el result entero, como prefieras
            return Ok(result.Data);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);

            // Igual que arriba: usar Succeeded
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(result.Data);
        }
    }
}
