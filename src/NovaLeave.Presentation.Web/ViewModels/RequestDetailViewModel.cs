using NovaLeave.Domain.Entities;

namespace NovaLeave.Presentation.Web.ViewModels;

// T213: ViewModel for displaying vacation request details in approver view (US2, Phase 4).
public class RequestDetailViewModel
{
    public Guid Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int RequestedDays { get; set; }
    public string Reason { get; set; } = string.Empty;
    public RequestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public int EmployeeCurrentBalance { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedByFullName { get; set; }
    public string? RejectionReason { get; set; }
}
