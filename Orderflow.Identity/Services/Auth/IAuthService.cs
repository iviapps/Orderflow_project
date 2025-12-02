using System.Security.Claims;                                // ClaimTypes, ClaimsPrincipal
using Orderflow.Identity.DTOs.Auth;                           // LoginRequest, RegisterRequest, LoginResponse, RegisterResponse, CurrentUserResponse
using Orderflow.Identity.DTOs;                                // ErrorResponse
using Orderflow.Identity.Services.Common;

namespace Orderflow.Identity.Services                          // Namespace de la capa de servicios
{
    // Interface que define qué operaciones de autenticación ofrece el servicio
    public interface IAuthService
    {
        // Login de usuario: devuelve un resultado con éxito/datos/error
        Task<AuthResult<LoginResponse>> LoginAsync(
            LoginRequest request);                             // DTO de entrada del login

        // Registro de usuario: idem, pero para alta de usuario
        Task<AuthResult<RegisterResponse>> RegisterAsync(
            RegisterRequest request);                          // DTO de entrada del registro

        // Obtener usuario actual a partir de los claims del token (aquí se usa userId directamente)
        Task<AuthResult<CurrentUserResponse>> GetCurrentUserAsync(
            string userId);                                   // Usuario actual (HttpContext.User)
    }
}
