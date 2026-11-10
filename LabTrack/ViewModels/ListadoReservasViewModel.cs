using LabTrack.Models;

namespace LabTrack.ViewModels;

public class ListadoReservasViewModel
{
    public List<Reserva> Reservas { get; set; } = [];
    public int Pagina { get; set; }
    public int TotalPaginas { get; set; }
    public int Total { get; set; }
    public EstadoReserva? Estado { get; set; }
    public string? Buscar { get; set; }
}
