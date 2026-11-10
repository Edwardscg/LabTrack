using LabTrack.Data;
using LabTrack.Models;
using LabTrack.Services;
using LabTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabTrack.Controllers;

[Authorize(Roles = Roles.Estudiante)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class ReservasController(LabTrackDbContext db, ReservaService service, UserManager<Usuario> users, LabClock clock) : Controller
{
    public async Task<IActionResult> Index(int pagina = 1)
    {
        var id = users.GetUserId(User);
        var query = db.Reservas.AsNoTracking().Where(r => r.EstudianteId == id);
        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / 15d));
        pagina = Math.Clamp(pagina, 1, pages);
        return View(new ListadoReservasViewModel
        {
            Total = total, Pagina = pagina, TotalPaginas = pages,
            Reservas = await query.Include(r => r.Equipo).OrderByDescending(r => r.FechaSolicitud)
                .ThenByDescending(r => r.Id).Skip((pagina - 1) * 15).Take(15).ToListAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Crear(int equipoId)
    {
        var model = new CrearReservaViewModel { EquipoId = equipoId, Fecha = DateOnly.FromDateTime(clock.Now) };
        return await LoadEquipmentAsync(model) ? View(model) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Crear(CrearReservaViewModel model)
    {
        if (!await LoadEquipmentAsync(model)) return NotFound();
        if (!ModelState.IsValid) return View(model);
        var result = await service.SolicitarAsync(users.GetUserId(User)!, model.EquipoId,
            model.Fecha!.Value.ToDateTime(model.HoraInicio!.Value), model.Fecha.Value.ToDateTime(model.HoraFin!.Value));
        if (!result.Exito)
        {
            ModelState.AddModelError("", result.Mensaje);
            return View(model);
        }
        TempData["Success"] = result.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> LoadEquipmentAsync(CrearReservaViewModel model)
    {
        var equipment = await db.Equipos.AsNoTracking().Include(e => e.Categoria).FirstOrDefaultAsync(e => e.Id == model.EquipoId);
        if (equipment is null) return false;
        model.Equipo = equipment;
        var now = clock.Now;
        model.ProximasReservas = await db.Reservas.AsNoTracking()
            .Where(r => r.EquipoId == model.EquipoId && r.Estado == EstadoReserva.Aprobada && r.Fin > now)
            .OrderBy(r => r.Inicio).Take(10).ToListAsync();
        return true;
    }
}
