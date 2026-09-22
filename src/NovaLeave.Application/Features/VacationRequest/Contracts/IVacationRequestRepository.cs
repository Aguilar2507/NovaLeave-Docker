using NovaLeave.Domain.Entities;

namespace NovaLeave.Application.Features.VacationRequest.Contracts;

// Contrato para persistir y consultar solicitudes de vacaciones (FR-005, FR-013).
public interface IVacationRequestRepository
{
    Task<Domain.Entities.VacationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Domain.Entities.VacationRequest>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    // Verifica si el empleado tiene solicitudes en Pending/Approved que se solapen con el rango dado (D-003).
    Task<bool> ExistsOverlappingAsync(
        Guid ownerId,
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludeRequestId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Domain.Entities.VacationRequest request, CancellationToken cancellationToken = default);

    // T212: Lista solicitudes Pending asignadas al aprobador, ordenadas por urgencia (ExpiryDate ASC).
    Task<IReadOnlyList<Domain.Entities.VacationRequest>> GetPendingForApproverAsync(
        Guid approverId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
