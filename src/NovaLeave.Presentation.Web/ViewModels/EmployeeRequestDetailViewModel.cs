using NovaLeave.Domain.Entities;

namespace NovaLeave.Presentation.Web.ViewModels;

// T512: ViewModel for employee's own request detail (US5, Phase 7).
// Includes Reason, resolved info, and action helpers.
public class EmployeeRequestDetailViewModel
{
    public Guid Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int RequestedDays { get; set; }
    public RequestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public string? ResolvedByFullName { get; set; }
    public string? RejectionReason { get; set; }

    // RejectionReason se reutiliza para motivo de anulación (Void). Este alias
    // proporciona una etiqueta semántica según el estado, sin renombrar la columna.
    public string? ResolutionReasonDisplay => RejectionReason;
    public string ResolutionReasonLabel => Status switch
    {
        RequestStatus.Rejected => "Motivo de Rechazo",
        RequestStatus.Voided => "Motivo de Anulación",
        _ => "Motivo de Resolución"
    };

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

    // Action availability helpers
    public bool CanCancel => Status == RequestStatus.Pending;
    public bool CanVoid => Status == RequestStatus.Approved && StartDate > DateOnly.FromDateTime(DateTime.UtcNow);
}
