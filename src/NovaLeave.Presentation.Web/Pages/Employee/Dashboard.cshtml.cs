using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NovaLeave.Application.Features.VacationRequest.List;
using NovaLeave.Infrastructure.Identity;

namespace NovaLeave.Presentation.Web.Pages.Employee;

// T108: Employee Dashboard — lista mis solicitudes con filtrado por estado y paginación.
// Consulta directamente el contexto para simplificar la lógica del dashboard (solo muestra últimas 5).
// Para listados completos con filtrado/paginación, se usará ListMyRequestsHandler en vistas dedicadas.
[Authorize(Roles = "User")]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ListMyRequestsHandler _listHandler;

    public DashboardModel(ApplicationDbContext context, ListMyRequestsHandler listHandler)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _listHandler = listHandler ?? throw new ArgumentNullException(nameof(listHandler));
    }

    public IReadOnlyList<Domain.Entities.VacationRequest> Requests { get; private set; } = Array.Empty<Domain.Entities.VacationRequest>();
    public int Balance { get; private set; }
    public string EmployeeName { get; private set; } = string.Empty;

    public async Task OnGetAsync()
    {
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            return;
        }

        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee == null)
        {
            return;
        }

        Balance = employee.Balance;
        EmployeeName = employee.Name;

        // Dashboard muestra últimas 5 solicitudes
        // Para listados completos con filtrado/paginación, usar ListMyRequestsHandler en vista dedicada.
        Requests = await _context.VacationRequests
            .Where(r => r.OwnerId == employeeId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync(HttpContext.RequestAborted);
    }

    private Guid GetEmployeeIdFromClaims()
    {
        var claim = User.FindFirst("EmployeeId");
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}

