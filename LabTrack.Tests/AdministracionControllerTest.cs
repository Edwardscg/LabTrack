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
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace LabTrack.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class AdministracionControllerTest
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

        private async Task<Usuario> CrearEstudianteAsync(string codigo)
        {
            var estudiante = new Usuario
            {
                UserName = codigo,
                CodigoEstudiante = codigo,
                NombreCompleto = "Estudiante de prueba",
                Email = $"{codigo}@prueba.local",
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

        private async Task<Categoria> CrearCategoriaAsync()
        {
            var categoria = new Categoria { Nombre = "Cómputo" };
            _contexto.Categorias.Add(categoria);
            await _contexto.SaveChangesAsync();
            return categoria;
        }

        private async Task<Equipo> CrearEquipoAsync(
            Categoria categoria,
            string codigo,
            string nombre)
        {
            var equipo = new Equipo
            {
                CodigoInventario = codigo,
                Nombre = nombre,
                Descripcion = "Equipo para pruebas.",
                CategoriaId = categoria.Id,
                Categoria = categoria
            };

            _contexto.Equipos.Add(equipo);
            await _contexto.SaveChangesAsync();
            return equipo;
        }

        private async Task<Reserva> CrearReservaAsync(
            Usuario estudiante,
            Equipo equipo,
            DateTime inicio,
            DateTime fin,
            EstadoReserva estado = EstadoReserva.Pendiente,
            Usuario? encargado = null)
        {
            var reserva = new Reserva
            {
                EstudianteId = estudiante.Id,
                Estudiante = estudiante,
                EquipoId = equipo.Id,
                Equipo = equipo,
                Inicio = inicio,
                Fin = fin,
                FechaSolicitud = _reloj.Now.AddHours(-1),
                Estado = estado,
                EncargadoId = encargado?.Id,
                Encargado = encargado,
                FechaDecision = encargado is null ? null : _reloj.Now
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

        // CASO DE PRUEBA (CP-11): Visualizar reservas pendientes
        [TestMethod]
        public async Task CP11_VisualizarReservasPendientes_MuestraDatosDeLaSolicitud()
        {
            // Preparación
            var estudiantePendiente = await CrearEstudianteAsync("C00111111");
            var estudianteAprobado = await CrearEstudianteAsync("C00222222");
            var encargado = await CrearEncargadoAsync();
            var categoria = await CrearCategoriaAsync();

            var equipoPendiente = await CrearEquipoAsync(
                categoria,
                "CMP-001",
                "Laptop pendiente");

            var equipoAprobado = await CrearEquipoAsync(
                categoria,
                "CMP-002",
                "Laptop aprobada");

            var fecha = DateOnly.FromDateTime(_reloj.Now.AddDays(1));
            var inicio = fecha.ToDateTime(new TimeOnly(9, 0));
            var fin = fecha.ToDateTime(new TimeOnly(10, 0));

            await CrearReservaAsync(
                estudiantePendiente,
                equipoPendiente,
                inicio,
                fin);

            await CrearReservaAsync(
                estudianteAprobado,
                equipoAprobado,
                inicio,
                fin,
                EstadoReserva.Aprobada,
                encargado);

            var controlador = CrearControlador(encargado.Id);

            // Ejecución
            var resultado = await controlador.Index();

            // Comprobación
            Assert.IsInstanceOfType(resultado, typeof(ViewResult));

            var modelo = (ListadoReservasViewModel)((ViewResult)resultado).Model!;
            Assert.AreEqual(EstadoReserva.Pendiente, modelo.Estado);
            Assert.AreEqual(1, modelo.Total);
            Assert.AreEqual(1, modelo.Reservas.Count);

            var reserva = modelo.Reservas.Single();
            Assert.AreEqual("Estudiante de prueba", reserva.Estudiante.NombreCompleto);
            Assert.AreEqual("Laptop pendiente", reserva.Equipo.Nombre);
            Assert.AreEqual("CMP-001", reserva.Equipo.CodigoInventario);
            Assert.AreEqual(inicio, reserva.Inicio);
            Assert.AreEqual(fin, reserva.Fin);
        }

        // CASO DE PRUEBA (CP-12): Aprobar una reserva pendiente
        [TestMethod]
        public async Task CP12_AprobarReservaPendiente_CambiaEstadoAprobada()
        {
            // Preparación
            var estudiante = await CrearEstudianteAsync("C00111111");
            var encargado = await CrearEncargadoAsync();
            var categoria = await CrearCategoriaAsync();
            var equipo = await CrearEquipoAsync(
                categoria,
                "CMP-001",
                "Laptop de prueba");

            var fecha = DateOnly.FromDateTime(_reloj.Now.AddDays(1));
            var reserva = await CrearReservaAsync(
                estudiante,
                equipo,
                fecha.ToDateTime(new TimeOnly(9, 0)),
                fecha.ToDateTime(new TimeOnly(10, 0)));

            var controlador = CrearControlador(encargado.Id);

            // Ejecución
            var resultado = await controlador.Aprobar(reserva.Id);

            // Comprobación
            Assert.IsInstanceOfType(resultado, typeof(RedirectToActionResult));

            var reservaActualizada = await _contexto.Reservas
                .SingleAsync(item => item.Id == reserva.Id);

            Assert.AreEqual(EstadoReserva.Aprobada, reservaActualizada.Estado);
            Assert.AreEqual(encargado.Id, reservaActualizada.EncargadoId);
            Assert.IsNotNull(reservaActualizada.FechaDecision);
        }

        // CASO DE PRUEBA (CP-13): Rechazar una solicitud con motivo
        [TestMethod]
        public async Task CP13_RechazarSolicitudConMotivo_RegistraElRechazo()
        {
            // Preparación
            var estudiante = await CrearEstudianteAsync("C00111111");
            var encargado = await CrearEncargadoAsync();
            var categoria = await CrearCategoriaAsync();
            var equipo = await CrearEquipoAsync(
                categoria,
                "CMP-001",
                "Laptop de prueba");

            var fecha = DateOnly.FromDateTime(_reloj.Now.AddDays(1));
            var reserva = await CrearReservaAsync(
                estudiante,
                equipo,
                fecha.ToDateTime(new TimeOnly(9, 0)),
                fecha.ToDateTime(new TimeOnly(10, 0)));

            var controlador = CrearControlador(encargado.Id);
            var motivo = "Equipo en mantenimiento.";

            // Ejecución
            var resultado = await controlador.Rechazar(new RechazarViewModel
            {
                Id = reserva.Id,
                Motivo = motivo
            });

            // Comprobación
            Assert.IsInstanceOfType(resultado, typeof(RedirectToActionResult));

            var reservaActualizada = await _contexto.Reservas
                .SingleAsync(item => item.Id == reserva.Id);

            Assert.AreEqual(EstadoReserva.Rechazada, reservaActualizada.Estado);
            Assert.AreEqual(motivo, reservaActualizada.MotivoRechazo);
            Assert.AreEqual(encargado.Id, reservaActualizada.EncargadoId);
        }

        // CASO DE PRUEBA (CP-14): Intentar rechazar una solicitud sin motivo
        [TestMethod]
        public async Task CP14_RechazarSolicitudSinMotivo_NoCompletaElRechazo()
        {
            // Preparación
            var estudiante = await CrearEstudianteAsync("C00111111");
            var encargado = await CrearEncargadoAsync();
            var categoria = await CrearCategoriaAsync();
            var equipo = await CrearEquipoAsync(
                categoria,
                "CMP-001",
                "Laptop de prueba");

            var fecha = DateOnly.FromDateTime(_reloj.Now.AddDays(1));
            var reserva = await CrearReservaAsync(
                estudiante,
                equipo,
                fecha.ToDateTime(new TimeOnly(9, 0)),
                fecha.ToDateTime(new TimeOnly(10, 0)));

            var controlador = CrearControlador(encargado.Id);
            var modelo = new RechazarViewModel
            {
                Id = reserva.Id,
                Motivo = ""
            };

            var errores = new List<ValidationResult>();
            Validator.TryValidateObject(
                modelo,
                new ValidationContext(modelo),
                errores,
                validateAllProperties: true);

            foreach (var error in errores)
            {
                foreach (var campo in error.MemberNames)
                {
                    controlador.ModelState.AddModelError(
                        campo,
                        error.ErrorMessage ?? "");
                }
            }

            // Ejecución
            var resultado = await controlador.Rechazar(modelo);

            // Comprobación
            Assert.IsInstanceOfType(resultado, typeof(ViewResult));

            var mensajes = controlador.ModelState.Values
                .SelectMany(estado => estado.Errors)
                .Select(error => error.ErrorMessage);

            Assert.IsTrue(mensajes.Any(mensaje =>
                mensaje.Contains("El motivo de rechazo es obligatorio.")));

            var reservaSinCambios = await _contexto.Reservas
                .SingleAsync(item => item.Id == reserva.Id);

            Assert.AreEqual(EstadoReserva.Pendiente, reservaSinCambios.Estado);
            Assert.IsNull(reservaSinCambios.MotivoRechazo);
        }
    }
}