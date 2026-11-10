namespace LabTrack.ViewModels;

public class DashboardViewModel
{
    public string Nombre { get; set; } = "";
    public bool Penalizado { get; set; }
    public int Pendientes { get; set; }
    public int Aprobadas { get; set; }
    public int Devueltas { get; set; }
    public int Equipos { get; set; }
}
