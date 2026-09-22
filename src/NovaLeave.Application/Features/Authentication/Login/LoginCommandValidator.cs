using FluentValidation;
using NovaLeave.Application.Features.Authentication.Login;

namespace NovaLeave.Application.Features.Authentication.Login;

/// <summary>
/// Validator for LoginCommand. Validates input format only (FR-004).
/// Business rules (account active status, lockout) are validated in the handler.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("Formato de correo electrónico inválido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");

        // ReturnUrl is optional but if provided must be a local URL (validated in handler/controller)
        RuleFor(x => x.ReturnUrl)
            .Must(BeLocalUrl).When(x => !string.IsNullOrWhiteSpace(x.ReturnUrl))
            .WithMessage("La URL de retorno no es válida.");
    }

    /// <summary>
    /// Validates that the ReturnUrl is a local path (not an external URL).
    /// Prevents open redirect attacks (SR-014).
    /// </summary>
    private static bool BeLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }
        // Only allow relative paths starting with /
        return url.StartsWith("/") && !url.StartsWith("//") && !url.Contains("://");
    }
}