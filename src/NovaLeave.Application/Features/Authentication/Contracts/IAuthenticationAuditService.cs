namespace NovaLeave.Application.Features.Authentication.Contracts;

// Contract for authentication audit logging service (FR-018)
// Responsible for persisting security-relevant authentication events
// MUST NOT log passwords, tokens, or sensitive PII (FR-019)
public interface IAuthenticationAuditService
{
    /// <summary>
    /// Logs a successful login event
    /// </summary>
    Task LogLoginSuccessAsync(
        string userId,
        string email,
        string role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a failed login attempt
    /// </summary>
    Task LogLoginFailureAsync(
        string email,
        string? userId = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a logout event
    /// </summary>
    Task LogLogoutAsync(
        string userId,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs account lockout event
    /// </summary>
    Task LogAccountLockedAsync(
        string userId,
        string email,
        CancellationToken cancellationToken = default);
}
