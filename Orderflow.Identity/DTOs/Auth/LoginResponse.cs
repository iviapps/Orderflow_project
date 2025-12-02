namespace Orderflow.Identity.DTOs.Auth
{
    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;    // JWT emitido
        public string TokenType { get; set; } = string.Empty;      // Normalmente "Bearer"
        public int ExpiresIn { get; set; }                         // Segundos hasta la expiración
        public string UserId { get; set; } = string.Empty;         // Id del usuario en Identity
        public string Email { get; set; } = string.Empty;          // Email del usuario
        public IEnumerable<string> Roles { get; set; } = [];       // Roles asociados
    }
}
