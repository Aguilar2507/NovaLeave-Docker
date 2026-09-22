using System.ComponentModel.DataAnnotations;

namespace NovaLeave.Presentation.Web.ViewModels;

// T114: ViewModel para crear una solicitud de vacaciones (US1, SR-002).
// Solo contiene los 3 campos que el usuario puede ingresar: StartDate, EndDate, Reason.
// Los campos calculados/servidor (RequestedDays, ExpiryDate, OwnerId) NO están aquí para evitar over-posting.
public sealed class CreateRequestViewModel
{
    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de inicio")]
    public DateOnly? StartDate { get; set; }

    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de fin")]
    public DateOnly? EndDate { get; set; }

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres.")]
    [Display(Name = "Motivo")]
    public string? Reason { get; set; }
}
