using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using NovaLeave.Domain.Entities;
using NovaLeave.Infrastructure.Identity;

namespace NovaLeave.Presentation.Web.Filters;

// Filtro que revalida el estado de la cuenta del empleado antes de ejecutar acciones
// Implementa FR-011: operaciones de cambio de estado solo permitidas para cuentas Active
// SR-004: Verifica estado en el servidor (no confía solo en claims del cliente)
public class ValidateAccountStatusFilter : IAsyncActionFilter
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;

    public ValidateAccountStatusFilter(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Solo aplicar en peticiones autenticadas
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            await next();
            return;
        }

        // Obtener usuario actual
        var user = await _userManager.GetUserAsync(context.HttpContext.User);
        if (user == null || !user.EmployeeId.HasValue)
        {
            // Usuario no encontrado o no tiene Employee asociado - rechazar
            context.Result = new RedirectToPageResult("/Account/Login", new { returnUrl = context.HttpContext.Request.Path });
            return;
        }

        // Obtener Employee asociado y verificar estado
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);

        if (employee == null)
        {
            // Employee no encontrado - rechazar
            context.Result = new RedirectToPageResult("/Account/Login", new { returnUrl = context.HttpContext.Request.Path });
            return;
        }

        // Verificar que la cuenta esté activa (FR-011)
        if (employee.Status != AccountStatus.Active)
        {
            // Cuenta inactiva - redirigir a login
            context.Result = new RedirectToPageResult("/Account/Login", new { returnUrl = context.HttpContext.Request.Path });
            return;
        }

        // Cuenta activa - continuar con la ejecución de la acción
        await next();
    }
}
