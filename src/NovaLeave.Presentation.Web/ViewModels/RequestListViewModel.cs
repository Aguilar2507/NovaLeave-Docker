using NovaLeave.Domain.Entities;

namespace NovaLeave.Presentation.Web.ViewModels;

// T511: ViewModel for employee request history list (US5, Phase 7).
// Excludes Reason from list per SR-010 (reason only visible in detail view).
public class RequestListViewModel
{
    public List<RequestSummaryViewModel> Requests { get; set; } = new();
    public int CurrentBalance { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

public class RequestSummaryViewModel
{
    public Guid Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int RequestedDays { get; set; }
    public RequestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateOnly ExpiryDate { get; set; }

    // Status display helpers (in Spanish per copilot-instructions.md)
    public string StatusDisplay => Status switch
    {
        RequestStatus.Pending => "Pendiente",
        RequestStatus.Approved => "Aprobada",
        RequestStatus.Rejected => "Rechazada",
        RequestStatus.Cancelled => "Cancelada",
        RequestStatus.Voided => "Anulada",
        _ => Status.ToString()
    };

    public string StatusCssClass => Status switch
    {
        RequestStatus.Pending => "badge bg-warning text-dark",
        RequestStatus.Approved => "badge bg-success",
        RequestStatus.Rejected => "badge bg-danger",
        RequestStatus.Cancelled => "badge bg-secondary",
        RequestStatus.Voided => "badge bg-dark",
        _ => "badge bg-light text-dark"
    };
}
