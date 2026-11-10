using System.ComponentModel.DataAnnotations;

namespace LabTrack.Models;

public class Categoria
{
    public int Id { get; set; }
    [Required, MaxLength(40)]
    public string Nombre { get; set; } = "";
}
