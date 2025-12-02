using FluentValidation;
using Orderflow.Identity.Controllers;
using Orderflow.Identity.DTOs.Auth;

// Validador para LoginRequest
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Regla para Email
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")                  // No puede ir vacío
            .EmailAddress().WithMessage("Email format is invalid")        // Debe tener formato de email
            .MaximumLength(256).WithMessage("Email is too long");         // Evitar emails más largos que la columna de Identity

        // Regla para Password
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")               // No puede ir vacía
            .MinimumLength(6).WithMessage("Password must be at least 6 characters long")
            // Longitud mínima
            .MaximumLength(100).WithMessage("Password is too long");      // Limitar longitud para evitar passwords absurdamente largas
                                                                          // Si quieres complejidad, puedes añadir un Matches() más abajo
    }
}
