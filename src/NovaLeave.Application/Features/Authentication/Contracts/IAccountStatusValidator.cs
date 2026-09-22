namespace NovaLeave.Application.Features.Authentication.Contracts;

// Contract for account status validation service (FR-003)
// Validates that an Employee account is Active before allowing authentication
public interface IAccountStatusValidator
{
    /// <summary>
    /// Validates that the Employee associated with the given IdentityId has Status == Active
    /// </summary>
    /// <param name="identityId">The ASP.NET Core Identity user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if account is Active, False otherwise</returns>
    Task<bool> IsAccountActiveAsync(
        string identityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the Employee record associated with the given IdentityId
    /// </summary>
    /// <param name="identityId">The ASP.NET Core Identity user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Employee if found, null otherwise</returns>
    Task<Domain.Entities.Employee?> GetEmployeeByIdentityIdAsync(
        string identityId,
        CancellationToken cancellationToken = default);
}
