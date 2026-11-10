using System.ComponentModel.DataAnnotations;

namespace LabTrack.Models;

public enum EstadoReserva { Pendiente, Aprobada, Rechazada, Devuelta }

public class Reserva
{
    public int Id { get; set; }
    [Required]
    public string EstudianteId { get; set; } = "";
    public Usuario Estudiante { get; set; } = null!;
    public int EquipoId { get; set; }
    public Equipo Equipo { get; set; } = null!;
    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public EstadoReserva Estado { get; set; } = EstadoReserva.Pendiente;
    public string? EncargadoId { get; set; }
    public Usuario? Encargado { get; set; }
    public DateTime? FechaDecision { get; set; }
    [MaxLength(500)]
    public string? MotivoRechazo { get; set; }
    public DateTime? FechaDevolucion { get; set; }
    [Timestamp]
    public byte[] Version { get; set; } = [];
}
