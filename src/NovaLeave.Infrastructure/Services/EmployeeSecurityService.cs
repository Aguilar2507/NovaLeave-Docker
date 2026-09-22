using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NovaLeave.Infrastructure.Identity;

namespace NovaLeave.Infrastructure.Services;

// Servicio que actualiza el security stamp del usuario cuando cambia el estado del empleado
// Implementa FR-010: invalidar sesión cuando cambian roles o estado de cuenta
public interface IEmployeeSecurityService
{
    Task UpdateSecurityStampOnStatusChangeAsync(Guid employeeId, CancellationToken cancellationToken = default);
}

public class EmployeeSecurityService : IEmployeeSecurityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;

    public EmployeeSecurityService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task UpdateSecurityStampOnStatusChangeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        // Buscar el ApplicationUser asociado al Employee
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.EmployeeId == employeeId, cancellationToken);

        if (user != null)
        {
            // Actualizar security stamp para invalidar sesiones activas
            await _userManager.UpdateSecurityStampAsync(user);
        }
    }
}
