using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Features.VacationRequest.Cancel;

// T310: Handler for cancelling a vacation request (US3, Phase 5).
// Transitions Pending → Cancelled, releases reservation, writes audit.
public class CancelRequestHandler
{
    private readonly IVacationRequestRepository _repository;
    private readonly IStateTransitionAuditService _auditService;

    public CancelRequestHandler(
        IVacationRequestRepository repository,
        IStateTransitionAuditService auditService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task Handle(
        CancelRequestCommand command,
        Employee employee,
        CancellationToken cancellationToken = default)
    {
        var request = await _repository.GetByIdAsync(command.RequestId, cancellationToken)
            ?? throw new InvalidOperationException($"Solicitud {command.RequestId} no encontrada.");

        if (request.OwnerId != employee.Id)
        {
            throw new InvalidOperationException(
                $"El empleado {employee.Id} no coincide con el propietario de la solicitud {request.OwnerId}.");
        }

        // Cancel the request (domain validates it's in Pending state)
        request.Cancel();

        // Restore the reserved days to the employee's balance
        employee.RestoreDays(request.RequestedDays);

        // Record state transition audit
        await _auditService.RecordTransitionAsync(
            request.Id,
            RequestStatus.Pending,
            RequestStatus.Cancelled,
            employee.Id.ToString(),
            "Employee",
            command.CorrelationId,
            null,
            request.Id.ToString(),
            cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);
    }
}
