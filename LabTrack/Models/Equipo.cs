using System.ComponentModel.DataAnnotations;

namespace LabTrack.Models;

public class Equipo
{
    public int Id { get; set; }
    [Required, MaxLength(30)]
    public string CodigoInventario { get; set; } = "";
    [Required, MaxLength(100)]
    public string Nombre { get; set; } = "";
    [Required, MaxLength(500)]
    public string Descripcion { get; set; } = "";
    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public ICollection<Reserva> Reservas { get; set; } = [];
}
