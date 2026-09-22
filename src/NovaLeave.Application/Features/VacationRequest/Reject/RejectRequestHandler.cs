using FluentValidation;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Features.VacationRequest.Reject;

// T211: Handler for rejecting a vacation request (US2, Phase 4).
public class RejectRequestHandler
{
    private readonly IVacationRequestRepository _repository;
    private readonly IStateTransitionAuditService _auditService;
    private readonly IValidator<RejectRequestCommand> _validator;

    public RejectRequestHandler(
        IVacationRequestRepository repository,
        IStateTransitionAuditService auditService,
        IValidator<RejectRequestCommand> validator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task Handle(
        RejectRequestCommand command,
        Employee employee,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ArgumentException($"Comando de rechazo no válido: {errors}");
        }

        var request = await _repository.GetByIdAsync(command.RequestId, cancellationToken)
            ?? throw new InvalidOperationException($"Solicitud {command.RequestId} no encontrada.");

        if (request.OwnerId != employee.Id)
        {
            throw new InvalidOperationException(
                $"El empleado {employee.Id} no coincide con el propietario de la solicitud {request.OwnerId}.");
        }

        request.Reject(command.ApproverId, command.RejectionReason);
        employee.RestoreDays(request.RequestedDays);

        await _auditService.RecordTransitionAsync(
            request.Id,
            RequestStatus.Pending,
            RequestStatus.Rejected,
            command.ApproverId.ToString(),
            "Approver",
            command.CorrelationId,
            reason: command.RejectionReason,
            requestId2: null,
            cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);
    }
}
