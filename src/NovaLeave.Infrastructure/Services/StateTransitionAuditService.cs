using System.Globalization;
using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Identity;

namespace NovaLeave.Infrastructure.Services;

// Servicio que persiste transiciones de estado de VacationRequest como AuditRecord (SR-008, FR-011).
public sealed class StateTransitionAuditService : IStateTransitionAuditService
{
    private readonly ApplicationDbContext _dbContext;

    public StateTransitionAuditService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task RecordTransitionAsync(
        Guid requestId,
        RequestStatus fromStatus,
        RequestStatus toStatus,
        string actorId,
        string actorRole,
        string correlationId,
        string? reason = null,
        string? requestId2 = null,
        CancellationToken cancellationToken = default)
    {
        var action = string.Create(CultureInfo.InvariantCulture, $"VacationRequest.{fromStatus}->{toStatus}");

        var record = new AuditRecord(
            action: action,
            result: "Success",
            correlationId: correlationId,
            actorId: actorId,
            actorRole: actorRole,
            entityType: nameof(VacationRequest),
            entityId: requestId.ToString(),
            reason: reason,
            requestId: requestId2);

        await _dbContext.AuditRecords.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }
}
