using LabTrack.Models;

namespace LabTrack.ViewModels;

public class CatalogoViewModel
{
    public int? CategoriaId { get; set; }
    public List<Categoria> Categorias { get; set; } = [];
    public List<EquipoResumen> Equipos { get; set; } = [];
    public int Pagina { get; set; }
    public int TotalPaginas { get; set; }
    public int Total { get; set; }
}

public class EquipoResumen
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Categoria { get; set; } = "";
    public bool Prestado { get; set; }
}
