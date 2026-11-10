using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace LabTrack.Models;

public class Usuario : IdentityUser
{
    [Required, MaxLength(120)]
    public string NombreCompleto { get; set; } = "";

    [RegularExpression(@"^C00[0-9]{6}$"), MaxLength(9)]
    public string? CodigoEstudiante { get; set; }

    public bool Activo { get; set; } = true;
    public bool Penalizado { get; set; }
}

public static class Roles
{
    public const string Estudiante = "Estudiante";
    public const string Encargado = "Encargado";
}
