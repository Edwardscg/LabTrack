using LabTrack.Data;
using LabTrack.Models;
using LabTrack.Services;
using LabTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabTrack.Controllers;

[Authorize(Roles = Roles.Encargado)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class AdministracionController(LabTrackDbContext db, ReservaService service, UserManager<Usuario> users, LabClock clock) : Controller
{
    public async Task<IActionResult> Index(EstadoReserva? estado = EstadoReserva.Pendiente, int pagina = 1)
    {
        if (estado.HasValue && !Enum.IsDefined(estado.Value)) return BadRequest();
        var query = db.Reservas.AsNoTracking();
        if (estado.HasValue) query = query.Where(r => r.Estado == estado);
        var model = await ListAsync(query, pagina);
        model.Estado = estado;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Aprobar(int id)
    {
        Notify(await service.AprobarAsync(id, users.GetUserId(User)!));
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Rechazar(int id)
    {
        var reservation = await PendingAsync(id);
        return reservation is null ? NotFound() : View(new RechazarViewModel { Id = id, Reserva = reservation });
    }

    [HttpPost]
    public async Task<IActionResult> Rechazar(RechazarViewModel model)
    {
        var reservation = await PendingAsync(model.Id);
        if (reservation is null) return NotFound();
        model.Reserva = reservation;
        if (!ModelState.IsValid) return View(model);
        var result = await service.RechazarAsync(model.Id, users.GetUserId(User)!, model.Motivo);
        if (!result.Exito)
        {
            ModelState.AddModelError("", result.Mensaje);
            return View(model);
        }
        Notify(result);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Devoluciones(string? buscar, int pagina = 1)
    {
        buscar = buscar?.Trim();
        if (buscar?.Length > 100) return BadRequest();
        var now = clock.Now;
        var query = db.Reservas.AsNoTracking().Where(r => r.Estado == EstadoReserva.Aprobada && r.Inicio <= now);
        if (!string.IsNullOrWhiteSpace(buscar))
            query = query.Where(r => r.Estudiante.NombreCompleto.Contains(buscar) ||
                r.Estudiante.CodigoEstudiante!.Contains(buscar) || r.Equipo.CodigoInventario.Contains(buscar) || r.Equipo.Nombre.Contains(buscar));
        var model = await ListAsync(query, pagina);
        model.Buscar = buscar;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Devolver(int id)
    {
        Notify(await service.DevolverAsync(id));
        return RedirectToAction(nameof(Devoluciones));
    }

    private Task<Reserva?> PendingAsync(int id) => db.Reservas.AsNoTracking().Include(r => r.Estudiante)
        .Include(r => r.Equipo).SingleOrDefaultAsync(r => r.Id == id && r.Estado == EstadoReserva.Pendiente);

    private void Notify(ResultadoReserva result) => TempData[result.Exito ? "Success" : "Error"] = result.Mensaje;

    private static async Task<ListadoReservasViewModel> ListAsync(IQueryable<Reserva> query, int page)
    {
        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / 15d));
        page = Math.Clamp(page, 1, pages);
        return new ListadoReservasViewModel
        {
            Pagina = page, TotalPaginas = pages, Total = total,
            Reservas = await query.Include(r => r.Estudiante).Include(r => r.Equipo)
                .OrderBy(r => r.Inicio).ThenBy(r => r.Id).Skip((page - 1) * 15).Take(15).ToListAsync()
        };
    }
}
