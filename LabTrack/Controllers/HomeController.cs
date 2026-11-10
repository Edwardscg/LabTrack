using System.Diagnostics;
using LabTrack.Data;
using LabTrack.Models;
using LabTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabTrack.Controllers;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class HomeController(LabTrackDbContext db, UserManager<Usuario> users) : Controller
{
    [Authorize(Roles = Roles.Estudiante + "," + Roles.Encargado)]
    public async Task<IActionResult> Index()
    {
        var user = await users.GetUserAsync(User);
        if (user is null || !user.Activo) return Forbid();
        var reservations = db.Reservas.AsNoTracking();
        if (User.IsInRole(Roles.Estudiante)) reservations = reservations.Where(r => r.EstudianteId == user.Id);
        return View(new DashboardViewModel
        {
            Nombre = user.NombreCompleto,
            Penalizado = user.Penalizado,
            Pendientes = await reservations.CountAsync(r => r.Estado == EstadoReserva.Pendiente),
            Aprobadas = await reservations.CountAsync(r => r.Estado == EstadoReserva.Aprobada),
            Devueltas = await reservations.CountAsync(r => r.Estado == EstadoReserva.Devuelta),
            Equipos = await db.Equipos.CountAsync()
        });
    }

    [AllowAnonymous]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    [AllowAnonymous]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code;
        ViewData["StatusCode"] = code;
        return View();
    }
}
