using System.Data;
using Microsoft.EntityFrameworkCore;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Identity;

namespace NovaLeave.Infrastructure.Persistence;

// T113: Repository para VacationRequest (AddAsync, ExistsOverlappingAsync con transacción Serializable para D-003).
public sealed class VacationRequestRepository : IVacationRequestRepository
{
    private readonly ApplicationDbContext _context;

    public VacationRequestRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Domain.Entities.VacationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.VacationRequests
            .Include(r => r.Owner)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Domain.Entities.VacationRequest>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await _context.VacationRequests
            .Where(r => r.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    // Verifica si existe solapamiento con Pending/Approved usando transacción Serializable (D-003).
    // Serializable evita race conditions cuando dos solicitudes se crean simultáneamente.
    public async Task<bool> ExistsOverlappingAsync(
        Guid ownerId,
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludeRequestId = null,
        CancellationToken cancellationToken = default)
    {
        // Si ya estamos en una transacción, reutilizarla; de lo contrario, crear Serializable.
        var existingTransaction = _context.Database.CurrentTransaction;

        if (existingTransaction is null)
        {
            // Crear nueva transacción Serializable para checkeo atómico de solapamiento (research.md §1 D-003)
            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false);

            var result = await QueryOverlapAsync(ownerId, startDate, endDate, excludeRequestId, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        else
        {
            // Reutilizar transacción existente
            return await QueryOverlapAsync(ownerId, startDate, endDate, excludeRequestId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<bool> QueryOverlapAsync(
        Guid ownerId,
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludeRequestId,
        CancellationToken cancellationToken)
    {
        var query = _context.VacationRequests
            .Where(r => r.OwnerId == ownerId)
            .Where(r => r.Status == RequestStatus.Pending || r.Status == RequestStatus.Approved)
            .Where(r => r.StartDate <= endDate && r.EndDate >= startDate);

        if (excludeRequestId.HasValue)
        {
            query = query.Where(r => r.Id != excludeRequestId.Value);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(Domain.Entities.VacationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _context.VacationRequests.AddAsync(request, cancellationToken).ConfigureAwait(false);
    }

    // T212: Lista solicitudes Pending para el aprobador, ordenadas por urgencia.
    public async Task<IReadOnlyList<Domain.Entities.VacationRequest>> GetPendingForApproverAsync(
        Guid approverId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.VacationRequests
            .Include(r => r.Owner)
            .Where(r => r.Status == RequestStatus.Pending)
            .Where(r => r.Owner.AssignedApproverId == approverId)
            .OrderBy(r => r.ExpiryDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
