using FluentValidation;

namespace NovaLeave.Application.Features.VacationRequest.Create;

// Validador de entrada para CreateRequestCommand (T111).
// Valida formato y reglas básicas. El handler luego valida invariantes de negocio (balance, overlap).
public sealed class CreateRequestValidator : AbstractValidator<CreateRequestCommand>
{
    public CreateRequestValidator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        RuleFor(x => x.OwnerId)
            .NotEmpty()
            .WithMessage("OwnerId es obligatorio.");

        RuleFor(x => x.StartDate)
            .GreaterThan(today)
            .WithMessage("La fecha de inicio debe ser estrictamente futura (posterior a hoy).");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("La fecha de fin debe ser posterior a la fecha de inicio.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo es obligatorio.")
            .MaximumLength(500)
            .WithMessage("El motivo no puede exceder 500 caracteres.");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId es obligatorio.");
    }
}
