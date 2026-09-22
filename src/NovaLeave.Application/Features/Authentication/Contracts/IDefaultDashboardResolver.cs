namespace NovaLeave.Application.Features.Authentication.Contracts;

/// <summary>
/// Resolves the default dashboard path based on the user's roles.
/// User role (including dual-role) → User Dashboard.
/// Approver-only → Approver Dashboard.
/// </summary>
public interface IDefaultDashboardResolver
{
    /// <summary>
    /// Returns the default redirect path for the given user.
    /// </summary>
    Task<string> GetDefaultDashboardPathAsync(
        string identityId,
        CancellationToken cancellationToken = default);
}
