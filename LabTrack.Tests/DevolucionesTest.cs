using LabTrack.Controllers;
using LabTrack.Data;
using LabTrack.Models;
using LabTrack.Services;
using LabTrack.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace LabTrack.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class DevolucionesTest
    {
        private const string CadenaConexion =
            @"Server=.\SQLEXPRESS;Database=LabTrackDb_Test;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        private ServiceProvider _proveedor = null!;
        private IServiceScope _alcance = null!;
        private LabTrackDbContext _contexto = null!;
        private UserManager<Usuario> _usuarios = null!;
        private ReservaService _servicio = null!;
        private LabClock _reloj = null!;

        [TestInitialize]
        public async Task InicializarBaseDePrueba()
        {
            var servicios = new ServiceCollection();
            servicios.AddLogging();
            servicios.AddDataProtection();
            servicios.AddHttpContextAccessor();
            servicios.AddControllersWithViews();
            servicios.AddDbContext<LabTrackDbContext>(opciones =>
                opciones.UseSqlServer(CadenaConexion));

            servicios.AddIdentity<Usuario, IdentityRole>(opciones =>
            {
                opciones.Lockout.MaxFailedAccessAttempts = 3;
                opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                opciones.Lockout.AllowedForNewUsers = true;
                opciones.Password.RequiredLength = 10;
            })
            .AddEntityFrameworkStores<LabTrackDbContext>()
            .AddDefaultTokenProviders();

            _proveedor = servicios.BuildServiceProvider();
            _alcance = _proveedor.CreateScope();

            _contexto = _alcance.ServiceProvider
                .GetRequiredService<LabTrackDbContext>();

            // Reinicia solo LabTrackDb_Test y crea su esquema desde las migraciones.
            await _contexto.Database.EnsureDeletedAsync();
            await _contexto.Database.MigrateAsync();

            _usuarios = _alcance.ServiceProvider
                .GetRequiredService<UserManager<Usuario>>();

            _reloj = new LabClock();
            _servicio = new ReservaService(_contexto, _reloj);
        }

        [TestCleanup]
        public void LimpiarServicios()
        {
            _alcance.Dispose();
            _proveedor.Dispose();
        }

        private async Task<Usuario> CrearEstudianteAsync()
        {
            var estudiante = new Usuario
            {
                UserName = "C00111111",
                CodigoEstudiante = "C00111111",
                NombreCompleto = "Estudiante de prueba",
                Email = "estudiante@prueba.local",
                Activo = true,
                LockoutEnabled = true
            };

            var creacion = await _usuarios.CreateAsync(estudiante, "Prueba123!");
            Assert.IsTrue(
                creacion.Succeeded,
                string.Join("; ", creacion.Errors.Select(error => error.Description)));

            return estudiante;
        }

        private async Task<Usuario> CrearEncargadoAsync()
        {
            var encargado = new Usuario
            {
                UserName = "encargado-prueba",
                NombreCompleto = "Encargado de prueba",
                Email = "encargado@prueba.local",
                Activo = true
            };

            var creacion = await _usuarios.CreateAsync(encargado, "Prueba123!");
            Assert.IsTrue(
                creacion.Succeeded,
                string.Join("; ", creacion.Errors.Select(error => error.Description)));

            return encargado;
        }

        private async Task<(Categoria Categoria, Equipo Equipo)> CrearEquipoAsync()
        {
            var categoria = new Categoria { Nombre = "Cómputo" };
            _contexto.Categorias.Add(categoria);
            await _contexto.SaveChangesAsync();

            var equipo = new Equipo
            {
                CodigoInventario = "CMP-001",
                Nombre = "Laptop de prueba",
                Descripcion = "Equipo para pruebas.",
                CategoriaId = categoria.Id,
                Categoria = categoria
            };

            _contexto.Equipos.Add(equipo);
            await _contexto.SaveChangesAsync();

            return (categoria, equipo);
        }

        private async Task<Reserva> CrearReservaAprobadaAsync(
            Usuario estudiante,
            Usuario encargado,
            Equipo equipo,
            DateTime inicio,
            DateTime fin)
        {
            var ahora = _reloj.Now;
            var reserva = new Reserva
            {
                EstudianteId = estudiante.Id,
                Estudiante = estudiante,
                EncargadoId = encargado.Id,
                Encargado = encargado,
                EquipoId = equipo.Id,
                Equipo = equipo,
                Inicio = inicio,
                Fin = fin,
                FechaSolicitud = ahora.AddHours(-2),
                FechaDecision = ahora.AddHours(-1),
                Estado = EstadoReserva.Aprobada
            };

            _contexto.Reservas.Add(reserva);
            await _contexto.SaveChangesAsync();
            return reserva;
        }

        private AdministracionController CrearControlador(string encargadoId)
        {
            var http = new DefaultHttpContext
            {
                RequestServices = _alcance.ServiceProvider,
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, encargadoId),
                            new Claim(ClaimTypes.Role, Roles.Encargado)
                        },
                        "Prueba"))
            };

            _alcance.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>()
                .HttpContext = http;

            var accion = new ActionContext(
                http,
                new RouteData(),
                new ControllerActionDescriptor());

            return new AdministracionController(
                _contexto,
                _servicio,
                _usuarios,
                _reloj)
            {
                ControllerContext = new ControllerContext(accion),
                Url = new UrlHelper(accion)
            };
        }

        // CASO DE PRUEBA (CP-15): Marcar la devolución de una reserva activa
        [TestMethod]
        public async Task CP15_MarcarDevolucionReservaActiva_RegistraDevolucion()
        {
            // Preparación
            var estudiante = await CrearEstudianteAsync();
            var encargado = await CrearEncargadoAsync();
            var (_, equipo) = await CrearEquipoAsync();

            var ahora = _reloj.Now;
            var reserva = await CrearReservaAprobadaAsync(
                estudiante,
                encargado,
                equipo,
                ahora.AddMinutes(-30),
                ahora.AddMinutes(30));

            var controlador = CrearControlador(encargado.Id);

            // Ejecución: se busca la reserva activa y se registra su devolución.
            var resultadoLista = await controlador.Devoluciones(null);

            Assert.IsInstanceOfType(resultadoLista, typeof(ViewResult));
            var listado = (ListadoReservasViewModel)((ViewResult)resultadoLista).Model!;
            Assert.AreEqual(1, listado.Total);
            Assert.AreEqual(reserva.Id, listado.Reservas.Single().Id);

            var resultadoDevolucion = await controlador.Devolver(reserva.Id);

            // Comprobación
            Assert.IsInstanceOfType(resultadoDevolucion, typeof(RedirectToActionResult));

            var reservaActualizada = await _contexto.Reservas
                .SingleAsync(item => item.Id == reserva.Id);

            Assert.AreEqual(EstadoReserva.Devuelta, reservaActualizada.Estado);
            Assert.IsNotNull(reservaActualizada.FechaDevolucion);
        }

        // CASO DE PRUEBA (CP-16): Verificar disponibilidad después de la devolución
        [TestMethod]
        public async Task CP16_DevolverEquipo_CambiaEstadoACatalogoDisponible()
        {
            // Preparación
            var estudiante = await CrearEstudianteAsync();
            var encargado = await CrearEncargadoAsync();
            var (categoria, equipo) = await CrearEquipoAsync();

            var ahora = _reloj.Now;
            var reserva = await CrearReservaAprobadaAsync(
                estudiante,
                encargado,
                equipo,
                ahora.AddMinutes(-30),
                ahora.AddMinutes(30));

            var controladorAdmin = CrearControlador(encargado.Id);
            var controladorEquipos = new EquiposController(_contexto, _reloj);

            var resultadoAntes = await controladorEquipos.Index(categoria.Id);
            var modeloAntes = (CatalogoViewModel)((ViewResult)resultadoAntes).Model!;
            Assert.IsTrue(modeloAntes.Equipos.Single().Prestado);

            // Ejecución
            await controladorAdmin.Devolver(reserva.Id);

            // Comprobación
            var resultadoDespues = await controladorEquipos.Index(categoria.Id);
            Assert.IsInstanceOfType(resultadoDespues, typeof(ViewResult));

            var modeloDespues = (CatalogoViewModel)((ViewResult)resultadoDespues).Model!;
            Assert.IsFalse(modeloDespues.Equipos.Single().Prestado);
        }

        // CASO DE PRUEBA (CP-17): Penalizar una devolución tardía
        [TestMethod]
        public async Task CP17_DevolverDespuesDelHorario_PenalizaAlEstudiante()
        {
            // Preparación
            var estudiante = await CrearEstudianteAsync();
            var encargado = await CrearEncargadoAsync();
            var (_, equipo) = await CrearEquipoAsync();

            var ahora = _reloj.Now;
            var reserva = await CrearReservaAprobadaAsync(
                estudiante,
                encargado,
                equipo,
                ahora.AddHours(-1),
                ahora.AddMinutes(-1));

            var controlador = CrearControlador(encargado.Id);

            // Ejecución
            var resultado = await controlador.Devolver(reserva.Id);

            // Comprobación
            Assert.IsInstanceOfType(resultado, typeof(RedirectToActionResult));

            var reservaActualizada = await _contexto.Reservas
                .SingleAsync(item => item.Id == reserva.Id);
            var estudianteActualizado = await _contexto.Users
                .SingleAsync(usuario => usuario.Id == estudiante.Id);

            Assert.AreEqual(EstadoReserva.Devuelta, reservaActualizada.Estado);
            Assert.IsNotNull(reservaActualizada.FechaDevolucion);
            Assert.IsTrue(estudianteActualizado.Penalizado);
        }
    }
}