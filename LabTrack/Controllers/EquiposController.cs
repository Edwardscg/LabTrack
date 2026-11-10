using LabTrack.Data;
using LabTrack.Models;
using LabTrack.Services;
using LabTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabTrack.Controllers;

[Authorize(Roles = Roles.Estudiante + "," + Roles.Encargado)]
public class EquiposController(LabTrackDbContext db, LabClock clock) : Controller
{
    public async Task<IActionResult> Index(int? categoriaId, int pagina = 1)
    {
        var query = db.Equipos.AsNoTracking();
        if (categoriaId.HasValue) query = query.Where(e => e.CategoriaId == categoriaId);
        var count = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(count / 12d));
        pagina = Math.Clamp(pagina, 1, pages);
        var now = clock.Now;
        var model = new CatalogoViewModel
        {
            CategoriaId = categoriaId,
            Categorias = await db.Categorias.AsNoTracking().OrderBy(c => c.Id).ToListAsync(),
            Total = count, Pagina = pagina, TotalPaginas = pages,
            Equipos = await query.OrderBy(e => e.CodigoInventario).Skip((pagina - 1) * 12).Take(12)
                .Select(e => new EquipoResumen
                {
                    Id = e.Id, Nombre = e.Nombre, Codigo = e.CodigoInventario,
                    Categoria = e.Categoria.Nombre, Descripcion = e.Descripcion,
                    Prestado = e.Reservas.Any(r => r.Estado == EstadoReserva.Aprobada && r.Inicio <= now)
                }).ToListAsync()
        };
        return View(model);
    }
}
