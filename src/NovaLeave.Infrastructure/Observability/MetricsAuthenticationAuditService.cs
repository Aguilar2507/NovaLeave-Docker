using NovaLeave.Application.Features.Authentication.Contracts;

namespace NovaLeave.Infrastructure.Observability;

// Decorator over IAuthenticationAuditService that counts authentication outcomes
// (spec_007 T202, FR-009, FR-010).
//
// SECURITY NOTE -- read before adding a label here.
// spec_004 requires that authentication failures never reveal whether an account exists, is
// inactive, or is locked out: every failure returns the same generic error. The inner service
// receives a `reason` for the audit trail, which is stored in the database behind
// authorization. Metrics are different: /metrics has no authentication in front of it by
// design, so a `reason` label would republish exactly the distinction spec_004 hides, on an
// unauthenticated endpoint. Only success/failure leaves this class.
public sealed class MetricsAuthenticationAuditService : IAuthenticationAuditService
{
    private readonly IAuthenticationAuditService _inner;
    private readonly NovaLeaveMetrics _metrics;

    public MetricsAuthenticationAuditService(
        IAuthenticationAuditService inner,
        NovaLeaveMetrics metrics)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public async Task LogLoginSuccessAsync(
        string userId,
        string email,
        string role,
        CancellationToken cancellationToken = default)
    {
        await _inner.LogLoginSuccessAsync(userId, email, role, cancellationToken).ConfigureAwait(false);
        RecordAttempt(succeeded: true);
    }

    public async Task LogLoginFailureAsync(
        string email,
        string? userId = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        await _inner.LogLoginFailureAsync(email, userId, reason, cancellationToken).ConfigureAwait(false);

        // `reason` and `email` are deliberately not forwarded to the metric. See the class note.
        RecordAttempt(succeeded: false);
    }

    // Logout and lockout are not authentication *attempts*, so they are not counted by the
    // attempts counter. They pass through unchanged. Counting lockouts separately would be a
    // reasonable follow-up, but it is outside spec_007's FR-009 and is not added here.
    public Task LogLogoutAsync(
        string userId,
        string email,
        CancellationToken cancellationToken = default) =>
        _inner.LogLogoutAsync(userId, email, cancellationToken);

    public Task LogAccountLockedAsync(
        string userId,
        string email,
        CancellationToken cancellationToken = default) =>
        _inner.LogAccountLockedAsync(userId, email, cancellationToken);

    // FR-011: a metrics failure must never affect authentication. The audit has already
    // completed by the time this runs.
    private void RecordAttempt(bool succeeded)
    {
        try
        {
            _metrics.RecordAuthenticationAttempt(succeeded);
        }
        catch (Exception)
        {
            // Intentionally ignored.
        }
    }
}
