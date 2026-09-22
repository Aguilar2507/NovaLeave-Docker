using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Features.VacationRequest.Void;

// T410: Handler for voiding an approved vacation request (US4, Phase 6).
// Validates StartDate > today, transitions Approved → Voided, restores days, records audit.
public class VoidRequestHandler
{
    private readonly IVacationRequestRepository _repository;
    private readonly IStateTransitionAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    public VoidRequestHandler(
        IVacationRequestRepository repository,
        IStateTransitionAuditService auditService,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    public async Task Handle(VoidRequestCommand command, Employee employee, CancellationToken cancellationToken)
    {
        var request = await _repository.GetByIdAsync(command.RequestId, cancellationToken)
            ?? throw new InvalidOperationException($"Solicitud {command.RequestId} no encontrada.");

        // Only owner can void
        if (request.OwnerId != employee.Id)
        {
            throw new InvalidOperationException("El empleado no coincide con el propietario de la solicitud.");
        }

        // Validate StartDate > today (using TimeProvider for testability)
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        if (request.StartDate <= today)
        {
            throw new InvalidOperationException(
                $"La solicitud no se puede anular porque ya ha comenzado. Fecha de inicio: {request.StartDate}, Hoy: {today}");
        }

        // Record old status for audit
        var oldStatus = request.Status;

        // Domain void transition (only Approved → Voided allowed)
        request.Void(employee.Id, command.Reason);

        // Restore days to employee balance
        employee.RestoreDays(request.RequestedDays);

        // Persist changes
        await _repository.SaveChangesAsync(cancellationToken);

        // Record audit
        await _auditService.RecordTransitionAsync(
            request.Id,
            oldStatus,
            request.Status,
            employee.Id.ToString(),
            "Employee",
            command.CorrelationId,
            command.Reason,
            request.Id.ToString(),
            cancellationToken);
    }
}
