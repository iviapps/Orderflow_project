using System.Security.Claims;                                // ClaimTypes, ClaimsPrincipal
using Orderflow.Identity.DTOs.Auth;                           // LoginRequest, RegisterRequest, LoginResponse, RegisterResponse, CurrentUserResponse
using Orderflow.Identity.DTOs;                                // ErrorResponse

namespace Orderflow.Identity.Services                          // Namespace de la capa de servicios
{
    // Interface que define qué operaciones de autenticación ofrece el servicio
    public interface IAuthService
    {
        // Login de usuario: devuelve un resultado con éxito/datos/error
        Task<AuthResult<LoginResponse>> LoginAsync(
            LoginRequest request,                             // DTO de entrada del login
            CancellationToken cancellationToken = default);   // Token de cancelación

        // Registro de usuario: idem, pero para alta de usuario
        Task<AuthResult<RegisterResponse>> RegisterAsync(
            RegisterRequest request,                          // DTO de entrada del registro
            CancellationToken cancellationToken = default);   // Token de cancelación

        // Obtener usuario actual a partir de los claims del token
        Task<AuthResult<CurrentUserResponse>> GetCurrentUserAsync(
            ClaimsPrincipal userPrincipal);                   // Usuario actual (HttpContext.User)
    }

    // Clase genérica para encapsular éxito/dato/error en las operaciones del servicio
    public class AuthResult<T>
    {
        // Indica si la operación ha ido bien
        public bool Success { get; set; }

        // Datos devueltos cuando Success = true
        public T? Data { get; set; }

        // Información de error cuando Success = false
        public ErrorResponse? Error { get; set; }

        // Factory estático para devolver éxito con datos
        public static AuthResult<T> Ok(T data) => new AuthResult<T>
        {
            Success = true,                                   // Marcamos operación como correcta
            Data = data                                       // Adjuntamos datos
        };

        // Factory estático para devolver fallo con error
        public static AuthResult<T> Fail(ErrorResponse error) => new AuthResult<T>
        {
            Success = false,                                  // Marcamos operación como fallida
            Error = error                                     // Adjuntamos el error
        };
    }
}
