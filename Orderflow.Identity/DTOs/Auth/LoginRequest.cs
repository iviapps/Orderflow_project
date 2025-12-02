namespace Orderflow.Identity.DTOs.Auth
{
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;          // Email del usuario
        public string Password { get; set; } = string.Empty;       // Password en texto plano (se encripta en el servidor)
    }

}
