using FluentValidation;

namespace NovaLeave.Application.Features.VacationRequest.Reject;

// T211: Validator for RejectRequestCommand (US2, Phase 4).
// Validates that RejectionReason is required and not empty (SR-006).
public class RejectRequestValidator : AbstractValidator<RejectRequestCommand>
{
    public RejectRequestValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty()
            .WithMessage("El ID de la solicitud es obligatorio.");

        RuleFor(x => x.ApproverId)
            .NotEmpty()
            .WithMessage("El ID del aprobador es obligatorio.");

        RuleFor(x => x.RejectionReason)
            .NotEmpty()
            .WithMessage("El motivo de rechazo es obligatorio.")
            .MaximumLength(500)
            .WithMessage("El motivo de rechazo no puede exceder 500 caracteres.");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("El ID de correlación es obligatorio.");
    }
}
