using Microsoft.AspNetCore.Identity;
using NovaLeave.Application.Features.Authentication.Contracts;
using NovaLeave.Infrastructure.Identity;

namespace NovaLeave.Infrastructure.Services;

// Implementa lógica de resolución de dashboard por rol (CU-202, D-007)
// Prioriza Employee dashboard si el usuario tiene rol User, incluso si también es Approver
public class DefaultDashboardResolver : IDefaultDashboardResolver
{
    private readonly UserManager<ApplicationUser> _userManager;

    public DefaultDashboardResolver(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<string> GetDefaultDashboardPathAsync(
        string identityId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(identityId);
        if (user == null)
        {
            return "/Account/Login";
        }

        var roles = await _userManager.GetRolesAsync(user);

        // Si el usuario tiene rol "User", prioriza Employee dashboard (incluso si también es Approver)
        if (roles.Contains("User"))
        {
            return "/Employee/Dashboard";
        }

        // Si solo tiene rol Approver
        if (roles.Contains("Approver"))
        {
            return "/Approver/Dashboard";
        }

        // Fallback a login si no tiene roles reconocidos
        return "/Account/Login";
    }
}
