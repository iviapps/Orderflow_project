namespace Orderflow.Identity.DTOs.Auth
{
    // Respuesta del endpoint exclusivo para Admin
    public record AdminOnlyResponse
    {
        public string Message { get; set; } = string.Empty;        // Mensaje de confirmación
        public DateTime Timestamp { get; set; }                    // Marca temporal del servidor
    }

}
