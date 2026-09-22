using System.ComponentModel.DataAnnotations;

namespace NovaLeave.Presentation.Web.ViewModels;

/// <summary>
/// ViewModel for the login form. Contains only Email and Password to prevent over-posting (SR-002).
/// Anti-forgery token is handled by the framework via FormTagHelper.
/// </summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido.")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Optional return URL for post-login redirection (FR-012).
    /// Validated server-side to prevent open redirects (SR-014).
    /// </summary>
    public string? ReturnUrl { get; set; }
}