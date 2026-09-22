using System.ComponentModel.DataAnnotations;

namespace NovaLeave.Presentation.Web.ViewModels;

// T603: ViewModel for editing a Pending vacation request (Phase 8, FR-009).
public class EditRequestViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    [Display(Name = "Fecha de inicio")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    [Display(Name = "Fecha de fin")]
    public DateOnly EndDate { get; set; }

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres.")]
    [Display(Name = "Motivo")]
    public string Reason { get; set; } = string.Empty;

    // Read-only display properties
    public int CurrentRequestedDays { get; set; }
    public int CurrentBalance { get; set; }
}
