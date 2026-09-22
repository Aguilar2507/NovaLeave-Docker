using FluentValidation;

namespace NovaLeave.Application.Features.VacationRequest.Edit;

// T602: Validator for EditRequestCommand (Phase 8, FR-009).
// Same business rules as CreateRequestValidator: future dates, valid range, reason required.
public class EditRequestValidator : AbstractValidator<EditRequestCommand>
{
    public EditRequestValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("El ID de la solicitud es obligatorio.");

        RuleFor(x => x.StartDate)
            .GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("La fecha de inicio debe ser futura.");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("La fecha de fin debe ser posterior a la fecha de inicio.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo es obligatorio.")
            .MaximumLength(500).WithMessage("El motivo no puede exceder 500 caracteres.");
    }
}
