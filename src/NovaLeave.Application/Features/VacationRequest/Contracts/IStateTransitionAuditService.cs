using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Features.VacationRequest.Contracts;

// Registra transiciones de estado de VacationRequest con detalles completos (SR-008, FR-011).
public interface IStateTransitionAuditService
{
    Task RecordTransitionAsync(
        Guid requestId,
        RequestStatus fromStatus,
        RequestStatus toStatus,
        string actorId,
        string actorRole,
        string correlationId,
        string? reason = null,
        string? requestId2 = null,
        CancellationToken cancellationToken = default);
}
