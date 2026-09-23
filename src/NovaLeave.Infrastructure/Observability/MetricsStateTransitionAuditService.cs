using NovaLeave.Application.Features.VacationRequest.Contracts;
using NovaLeave.Domain.Entities;

namespace NovaLeave.Infrastructure.Observability;

// Decorator over IStateTransitionAuditService that counts vacation request state transitions
// (spec_007 T201, FR-006, FR-010).
//
// Why a decorator: all six handlers -- Create, Approve, Reject, Cancel, Void, Edit -- already
// funnel through RecordTransitionAsync, which receives both the originating and resulting
// status. Wrapping that single chokepoint captures every transition, including any added later,
// without editing one line of business code (research.md R-002).
public sealed class MetricsStateTransitionAuditService : IStateTransitionAuditService
{
    private readonly IStateTransitionAuditService _inner;
    private readonly NovaLeaveMetrics _metrics;

    public MetricsStateTransitionAuditService(
        IStateTransitionAuditService inner,
        NovaLeaveMetrics metrics)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
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
        // Audit first. It is the obligation (Constitution §8); the metric is a side channel.
        // If the audit throws, no metric is recorded -- correct, because the transition did not
        // happen either.
        await _inner.RecordTransitionAsync(
            requestId, fromStatus, toStatus, actorId, actorRole, correlationId,
            reason, requestId2, cancellationToken).ConfigureAwait(false);

        // FR-011: observability must never be the cause of an incident. A failure in the metrics
        // path is swallowed deliberately -- by this point the audit has already succeeded, and
        // failing the user's action because a counter misbehaved would be indefensible.
        try
        {
            _metrics.RecordTransition(fromStatus, toStatus);
        }
        catch (Exception)
        {
            // Intentionally ignored. Note what is NOT passed to the metric: requestId, actorId,
            // correlationId and reason all stay here. Only the two bounded enum values leave
            // (Constitution §7.3, research.md R-005).
        }
    }
}
