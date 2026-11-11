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
    public class ReservasControllerTest
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

        private async Task CrearReservaAprobadaAsync(
            Usuario estudiante,
            Usuario encargado,
            Equipo equipo,
            DateTime inicio,
            DateTime fin)
        {
            var ahora = _reloj.Now;

            _contexto.Reservas.Add(new Reserva
            {
                EstudianteId = estudiante.Id,
                Estudiante = estudiante,
                EncargadoId = encargado.Id,
                Encargado = encargado,
                EquipoId = equipo.Id,
                Equipo = equipo,
                Inicio = inicio,
                Fin = fin,
                FechaSolicitud = ahora.AddHours(-1),
                FechaDecision = ahora.AddMinutes(-30),
                Estado = EstadoReserva.Aprobada
            });

            await _contexto.SaveChangesAsync();
        }

        private ReservasController CrearControlador(string estudianteId)
        {
            var http = new DefaultHttpContext
            {
                RequestServices = _alcance.ServiceProvider,
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, estudianteId),
                            new Claim(ClaimTypes.Role, Roles.Estudiante)
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

            return new ReservasController(
                _contexto,
                _servicio,
                _usuarios,
                _reloj)
            {
                ControllerContext = new ControllerContext(accion),
                Url = new UrlHelper(accion)
            };
        }

        // CASO DE PRUEBA (CP-08): Solicitar la reserva de un equipo disponible
        [TestMethod]
        public async Task CP08_SolicitarReservaEquipoDisponible_RegistraSolicitud()
        {
            // Preparación
            var estudiante = await CrearEstudianteAsync("C00111111");
            var categoria = await CrearCategoriaAsync();
            var equipo = await CrearEquipoAsync(
                categoria,
                "CMP-001",
                "Laptop de prueba");

            var fecha = DateOnly.FromDateTime(_reloj.Now.AddDays(1));
            var controlador = CrearControlador(estudiante.Id);

            // Ejecución
            var resultado = await controlador.Crear(new CrearReservaViewModel
            {
                EquipoId = equipo.Id,
                Fecha = fecha,
                HoraInicio = new TimeOnly(9, 0),
                HoraFin = new TimeOnly(10, 0)
            });

            // Comprobación
            Assert.IsInstanceOfType(resultado, typeof(RedirectToActionResult));

            var reserva = await _contexto.Reservas.SingleAsync();
            Assert.AreEqual(estudiante.Id, reserva.EstudianteId);
            Assert.AreEqual(equipo.Id, reserva.EquipoId);
            Assert.AreEqual(EstadoReserva.Pendiente, reserva.Estado);
            Assert.AreEqual(fecha.ToDateTime(new TimeOnly(9, 0)), reserva.Inicio);
            Assert.AreEqual(fecha.ToDateTime(new TimeOnly(10, 0)), reserva.Fin);
        }

        // CASO DE PRUEBA (CP-09): Intentar reservar un equipo con horario cruzado
        [TestMethod]
        public async Task CP09_ReservaConHorarioCruzado_RechazaSolicitud()
        {
            // Preparación
            var estudiante = await CrearEstudianteAsync("C00111111");
            var otroEstudiante = await CrearEstudianteAsync("C00222222");
            var encargado = await CrearEncargadoAsync();
            var categoria = await CrearCategoriaAsync();
            var equipo = await CrearEquipoAsync(
                categoria,
                "CMP-001",
                "Laptop de prueba");

            var fecha = DateOnly.FromDateTime(_reloj.Now.AddDays(1));
            var inicioExistente = fecha.ToDateTime(new TimeOnly(9, 0));
            var finExistente = fecha.ToDateTime(new TimeOnly(10, 0));

            await CrearReservaAprobadaAsync(
                otroEstudiante,
                encargado,
                equipo,
                inicioExistente,
                finExistente);

            var controlador = CrearControlador(estudiante.Id);

            // Ejecución
            var resultado = await controlador.Crear(new CrearReservaViewModel
            {
                EquipoId = equipo.Id,
                Fecha = fecha,
                HoraInicio = new TimeOnly(9, 30),
                HoraFin = new TimeOnly(10, 30)
            });

            // Comprobación
            Assert.IsInstanceOfType(resultado, typeof(ViewResult));

            var mensajes = controlador.ModelState.Values
                .SelectMany(estado => estado.Errors)
                .Select(error => error.ErrorMessage);

            Assert.IsTrue(mensajes.Any(mensaje =>
                mensaje.Contains("El equipo ya tiene una reserva aprobada")));

            Assert.AreEqual(1, await _contexto.Reservas.CountAsync());
        }

        // CASO DE PRUEBA (CP-10): Intentar tener más de un equipo reservado simultáneamente
        [TestMethod]
        public async Task CP10_EstudianteConOtraReservaEnElHorario_RechazaSolicitud()
        {
            // Preparación
            var estudiante = await CrearEstudianteAsync("C00333333");
            var encargado = await CrearEncargadoAsync();
            var categoria = await CrearCategoriaAsync();

            var equipoReservado = await CrearEquipoAsync(
                categoria,
                "CMP-001",
                "Laptop reservada");

            var equipoSolicitado = await CrearEquipoAsync(
                categoria,
                "CMP-002",
                "Laptop solicitada");

            var fecha = DateOnly.FromDateTime(_reloj.Now.AddDays(1));
            var inicioExistente = fecha.ToDateTime(new TimeOnly(9, 0));
            var finExistente = fecha.ToDateTime(new TimeOnly(10, 0));

            await CrearReservaAprobadaAsync(
                estudiante,
                encargado,
                equipoReservado,
                inicioExistente,
                finExistente);

            var controlador = CrearControlador(estudiante.Id);

            // Ejecución
            var resultado = await controlador.Crear(new CrearReservaViewModel
            {
                EquipoId = equipoSolicitado.Id,
                Fecha = fecha,
                HoraInicio = new TimeOnly(9, 30),
                HoraFin = new TimeOnly(10, 30)
            });

            // Comprobación
            Assert.IsInstanceOfType(resultado, typeof(ViewResult));

            var mensajes = controlador.ModelState.Values
                .SelectMany(estado => estado.Errors)
                .Select(error => error.ErrorMessage);

            Assert.IsTrue(mensajes.Any(mensaje =>
                mensaje.Contains("Ya tienes una solicitud pendiente o aprobada en ese horario.")));

            Assert.AreEqual(1, await _contexto.Reservas.CountAsync());
        }
    }
}