namespace Orderflow.Identity.DTOs.Auth
{
    public class CurrentUserResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public IEnumerable<string> Roles { get; set; } = [];       // Roles del usuario actual
    }
}
