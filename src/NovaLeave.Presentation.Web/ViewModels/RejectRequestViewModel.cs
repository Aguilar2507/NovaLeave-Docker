using System.ComponentModel.DataAnnotations;

namespace NovaLeave.Presentation.Web.ViewModels;

// T213: ViewModel for rejecting a vacation request (US2, Phase 4).
// Rejection reason is mandatory (SR-006).
public class RejectRequestViewModel
{
    [Required(ErrorMessage = "El motivo de rechazo es obligatorio.")]
    [StringLength(500, ErrorMessage = "El motivo de rechazo no puede exceder 500 caracteres.")]
    public string RejectionReason { get; set; } = string.Empty;

    public Guid RequestId { get; set; }
}
