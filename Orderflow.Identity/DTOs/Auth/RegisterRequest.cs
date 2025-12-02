namespace Orderflow.Identity.DTOs.Auth
{
    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;          // Email que se registrará
        public string Password { get; set; } = string.Empty;       // Password
        public string ConfirmPassword { get; set; } = string.Empty;// Confirmación (validada vía FluentValidation)
    }
}
