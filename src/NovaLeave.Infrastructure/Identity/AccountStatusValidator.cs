using Microsoft.EntityFrameworkCore;
using NovaLeave.Application.Features.Authentication.Contracts;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Identity;

namespace NovaLeave.Infrastructure.Identity;

// Implementation of IAccountStatusValidator
// Checks Employee.Status == Active via IdentityId linkage (FR-003)
public class AccountStatusValidator : IAccountStatusValidator
{
    private readonly ApplicationDbContext _context;

    public AccountStatusValidator(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<bool> IsAccountActiveAsync(
        string identityId,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetEmployeeByIdentityIdAsync(identityId, cancellationToken);

        // Return true only if Employee exists and Status == Active
        return employee != null && employee.Status == AccountStatus.Active;
    }

    public async Task<Employee?> GetEmployeeByIdentityIdAsync(
        string identityId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Employees
            .FirstOrDefaultAsync(e => e.IdentityId == identityId, cancellationToken);
    }
}
