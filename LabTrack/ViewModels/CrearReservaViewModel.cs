using System.ComponentModel.DataAnnotations;
using LabTrack.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace LabTrack.ViewModels;

public class CrearReservaViewModel : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un equipo válido.")]
    public int EquipoId { get; set; }
    [Required(ErrorMessage = "Selecciona la fecha."), DataType(DataType.Date)]
    public DateOnly? Fecha { get; set; }
    [Required(ErrorMessage = "Selecciona la hora de inicio."), DataType(DataType.Time)]
    [Display(Name = "Hora de inicio")]
    public TimeOnly? HoraInicio { get; set; }
    [Required(ErrorMessage = "Selecciona la hora de fin."), DataType(DataType.Time)]
    [Display(Name = "Hora de fin")]
    public TimeOnly? HoraFin { get; set; }
    [BindNever, ValidateNever]
    public Equipo Equipo { get; set; } = null!;
    [BindNever, ValidateNever]
    public List<Reserva> ProximasReservas { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (HoraInicio.HasValue && HoraFin.HasValue && HoraFin <= HoraInicio)
            yield return new ValidationResult("La hora de fin debe ser posterior a la hora de inicio.", [nameof(HoraFin)]);
    }
}
