using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Features.VacationRequest.Edit;

// T602: Handler for editing a Pending vacation request (Phase 8, FR-009).
// Validates ownership, Pending status, re-checks overlap/balance, adjusts employee balance delta, writes audit.
public class EditRequestHandler
{
    private readonly IVacationRequestRepository _repository;
    private readonly IStateTransitionAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    public EditRequestHandler(
        IVacationRequestRepository repository,
        IStateTransitionAuditService auditService,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(EditRequestCommand command, Employee employee, CancellationToken cancellationToken)
    {
        var request = await _repository.GetByIdAsync(command.RequestId, cancellationToken)
            ?? throw new InvalidOperationException($"Solicitud {command.RequestId} no encontrada.");

        // Only owner can edit
        if (request.OwnerId != employee.Id)
        {
            throw new InvalidOperationException("El empleado no coincide con el propietario de la solicitud.");
        }

        // Only Pending requests can be edited (FR-009)
        if (request.Status != RequestStatus.Pending)
        {
            throw new InvalidOperationException("Solo las solicitudes en estado Pending pueden ser editadas.");
        }

        // Calculate new requested days
        var newRequestedDays = (command.EndDate.DayNumber - command.StartDate.DayNumber) + 1;

        // Re-check overlap excluding this request (D-003)
        var hasOverlap = await _repository.ExistsOverlappingAsync(
            employee.Id,
            command.StartDate,
            command.EndDate,
            excludeRequestId: command.RequestId,
            cancellationToken);

        if (hasOverlap)
        {
            throw new InvalidOperationException(
                $"Ya existe una solicitud aprobada o pendiente que se solapa con el rango {command.StartDate:yyyy-MM-dd} a {command.EndDate:yyyy-MM-dd}.");
        }

        // Calculate balance delta and validate sufficiency
        var oldDays = request.RequestedDays;
        var daysDelta = newRequestedDays - oldDays;

        if (daysDelta > 0)
        {
            // Requesting more days - check balance
            if (employee.Balance < daysDelta)
            {
                throw new InvalidOperationException(
                    $"Saldo insuficiente. Necesita {daysDelta} días adicionales, pero solo tiene {employee.Balance} disponibles.");
            }
            employee.DeductDays(daysDelta);
        }
        else if (daysDelta < 0)
        {
            // Requesting fewer days - restore balance
            employee.RestoreDays(Math.Abs(daysDelta));
        }
        // If daysDelta == 0, no balance change needed

        // Update request (domain entity method)
        request.Edit(command.StartDate, command.EndDate, command.Reason, newRequestedDays);

        // Persist changes
        await _repository.SaveChangesAsync(cancellationToken);

        // Record audit (Pending → Pending with edit details)
        var auditMessage = $"Solicitud editada. Nuevas fechas: {command.StartDate:yyyy-MM-dd} a {command.EndDate:yyyy-MM-dd}, días: {newRequestedDays}.";
        await _auditService.RecordTransitionAsync(
            request.Id,
            RequestStatus.Pending,
            RequestStatus.Pending,
            employee.Id.ToString(),
            "Employee",
            command.CorrelationId,
            auditMessage,
            request.Id.ToString(),
            cancellationToken);
    }
}
