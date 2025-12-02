namespace Orderflow.Identity.DTOs.Auth
{

    // Respuesta al registrarse
    public class RegisterResponse
    {
        public string UserId { get; set; } = string.Empty;         // Id del usuario creado
        public string Email { get; set; } = string.Empty;          // Email del usuario
        public string Message { get; set; } = string.Empty;        // Mensaje de confirmación
    }
}
