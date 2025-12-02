namespace Orderflow.Identity.DTOs
{
    public class ErrorResponse
    {
        // Lista de mensajes de error que se devuelven al cliente
        public List<string> Errors { get; set; }

        // Código HTTP del error (por ejemplo: 400, 401, 500...)
        public int StatusCode { get; set; }

        // Fecha y hora del error para trazabilidad
        public DateTime Timestamp { get; set; }

        // Constructor principal que inyecta los errores
        public ErrorResponse(IEnumerable<string> errors, int statusCode = 400)
        {
            Errors = errors.ToList();
            StatusCode = statusCode;
            Timestamp = DateTime.UtcNow;
        }
    }
}