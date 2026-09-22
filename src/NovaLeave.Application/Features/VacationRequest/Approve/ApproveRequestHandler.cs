using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Features.VacationRequest.Approve;

// T210: Handler for approving a vacation request (US2, Phase 4).
public class ApproveRequestHandler
{
    private readonly IVacationRequestRepository _repository;
    private readonly IStateTransitionAuditService _auditService;

    public ApproveRequestHandler(
        IVacationRequestRepository repository,
        IStateTransitionAuditService auditService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task Handle(
        ApproveRequestCommand command,
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

        if (employee.Balance < request.RequestedDays)
        {
            throw new InvalidOperationException(
                $"Saldo insuficiente para aprobar la solicitud. Saldo actual: {employee.Balance}, días solicitados: {request.RequestedDays}.");
        }

        request.Approve(command.ApproverId);
        employee.DeductDays(request.RequestedDays);

        await _auditService.RecordTransitionAsync(
            request.Id,
            RequestStatus.Pending,
            RequestStatus.Approved,
            command.ApproverId.ToString(),
            "Approver",
            command.CorrelationId,
            reason: null,
            requestId2: null,
            cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);
    }
}
