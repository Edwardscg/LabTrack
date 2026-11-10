using System.ComponentModel.DataAnnotations;
using LabTrack.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace LabTrack.ViewModels;

public class RechazarViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El motivo de rechazo es obligatorio.")]
    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    [Display(Name = "Motivo de rechazo")]
    public string Motivo { get; set; } = "";
    [BindNever, ValidateNever]
    public Reserva Reserva { get; set; } = null!;
}
