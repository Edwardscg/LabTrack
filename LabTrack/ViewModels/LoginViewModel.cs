using System.ComponentModel.DataAnnotations;

namespace LabTrack.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Ingresa tu identificador."), StringLength(64)]
    [Display(Name = "Código de estudiante o identificador")]
    public string Identificador { get; set; } = "";
    [Required(ErrorMessage = "Ingresa tu contraseña."), DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
