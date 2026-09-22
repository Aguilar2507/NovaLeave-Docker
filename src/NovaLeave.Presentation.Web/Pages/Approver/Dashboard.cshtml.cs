using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NovaLeave.Application.Features.VacationRequest.List;

namespace NovaLeave.Presentation.Web.Pages.Approver;

// T109: Approver Dashboard — lista solicitudes Pending asignadas a mí.
// Muestra estadísticas de pending y urgentes, ordenado por urgencia (ExpiryDate asc).
[Authorize(Roles = "Approver")]
public class DashboardModel : PageModel
{
    private readonly ListPendingForApproverHandler _listHandler;
    private readonly TimeProvider _timeProvider;

    public DashboardModel(ListPendingForApproverHandler listHandler, TimeProvider timeProvider)
    {
        _listHandler = listHandler ?? throw new ArgumentNullException(nameof(listHandler));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public IReadOnlyList<Domain.Entities.VacationRequest> PendingRequests { get; private set; } = Array.Empty<Domain.Entities.VacationRequest>();
    public int UrgentCount { get; private set; }

    public async Task OnGetAsync()
    {
        var employeeId = GetEmployeeIdFromClaims();
        if (employeeId == Guid.Empty)
        {
            return;
        }

        var query = new ListPendingForApproverQuery(employeeId, 1, 20);
        PendingRequests = await _listHandler.Handle(query, HttpContext.RequestAborted);

        // Calcular solicitudes urgentes (expiran en <= 2 días)
        var now = DateOnly.FromDateTime(_timeProvider.GetUtcNow().Date);
        UrgentCount = PendingRequests.Count(r =>
        {
            var daysRemaining = r.ExpiryDate.DayNumber - now.DayNumber;
            return daysRemaining >= 0 && daysRemaining <= 2;
        });
    }

    // Helper: calcula días restantes hasta expiración
    public int GetDaysUntilExpiry(Domain.Entities.VacationRequest request)
    {
        var now = DateOnly.FromDateTime(_timeProvider.GetUtcNow().Date);
        var daysRemaining = request.ExpiryDate.DayNumber - now.DayNumber;

        return daysRemaining >= 0 ? daysRemaining : 0;
    }

    // Helper: determina si una solicitud es urgente (expira en <= 2 días)
    public bool IsUrgent(Domain.Entities.VacationRequest request)
    {
        var daysRemaining = GetDaysUntilExpiry(request);
        return daysRemaining <= 2;
    }

    private Guid GetEmployeeIdFromClaims()
    {
        var claim = User.FindFirst("EmployeeId");
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }
}

