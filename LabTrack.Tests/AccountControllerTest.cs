using LabTrack.Controllers;
using LabTrack.Data;
using LabTrack.Models;
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

namespace LabTrack.Tests
{
    [TestClass]
    public class AccountControllerTest
    {
        // CASO DE PRUEBA (CP-01): Iniciar sesión con credenciales válidas
        [TestMethod]
        public async Task CP01_IniciarSesionConCredencialesValidas_RedireccionaAlPanel()
        {
            // Preparación: servicios de Identity y base temporal en memoria.
            var servicios = new ServiceCollection();
            servicios.AddLogging();
            servicios.AddDataProtection();
            servicios.AddHttpContextAccessor();
            servicios.AddDbContext<LabTrackDbContext>(opciones =>
                opciones.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            servicios.AddIdentity<Usuario, IdentityRole>()
                .AddEntityFrameworkStores<LabTrackDbContext>()
                .AddDefaultTokenProviders();

            using var proveedor = servicios.BuildServiceProvider();
            using var alcance = proveedor.CreateScope();

            var contexto = alcance.ServiceProvider.GetRequiredService<LabTrackDbContext>();
            await contexto.Database.EnsureCreatedAsync();

            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            const string codigo = "C00123456";
            const string contrasena = "Prueba123!";

            var usuario = new Usuario
            {
                UserName = codigo,
                CodigoEstudiante = codigo,
                NombreCompleto = "Estudiante de prueba",
                Email = "estudiante@prueba.local",
                Activo = true
            };

            var creacion = await usuarios.CreateAsync(usuario, contrasena);
            Assert.IsTrue(
                creacion.Succeeded,
                string.Join("; ", creacion.Errors.Select(error => error.Description)));

            // Ejecución: se envían credenciales válidas al controlador.
            var http = new DefaultHttpContext
            {
                RequestServices = alcance.ServiceProvider
            };
            alcance.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>()
                .HttpContext = http;

            var accion = new ActionContext(
                http,
                new RouteData(),
                new ControllerActionDescriptor());

            var controlador = new AccountController(
                alcance.ServiceProvider.GetRequiredService<SignInManager<Usuario>>(),
                usuarios)
            {
                ControllerContext = new ControllerContext(accion),
                Url = new UrlHelper(accion)
            };

            var resultado = await controlador.Login(new LoginViewModel
            {
                Identificador = codigo,
                Password = contrasena
            });

            // Comprobación: el estudiante es dirigido al panel principal.
            Assert.IsInstanceOfType(resultado, typeof(RedirectToActionResult));

            var redireccion = (RedirectToActionResult)resultado;
            Assert.AreEqual("Index", redireccion.ActionName);
            Assert.AreEqual("Home", redireccion.ControllerName);
        }

        // CASO DE PRUEBA (CP-02): Iniciar sesión con credenciales incorrectas
        [TestMethod]
        public async Task CP02_IniciarSesionConCredencialesIncorrectas_MuestraError()
        {
            // Preparación: se crea un usuario válido en una base temporal en memoria.
            var servicios = new ServiceCollection();
            servicios.AddLogging();
            servicios.AddDataProtection();
            servicios.AddHttpContextAccessor();
            servicios.AddControllersWithViews();
            servicios.AddDbContext<LabTrackDbContext>(opciones =>
                opciones.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            servicios.AddIdentity<Usuario, IdentityRole>()
                .AddEntityFrameworkStores<LabTrackDbContext>()
                .AddDefaultTokenProviders();

            using var proveedor = servicios.BuildServiceProvider();
            using var alcance = proveedor.CreateScope();

            var contexto = alcance.ServiceProvider.GetRequiredService<LabTrackDbContext>();
            await contexto.Database.EnsureCreatedAsync();

            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            const string codigo = "C00123456";
            const string contrasenaCorrecta = "Prueba123!";

            var usuario = new Usuario
            {
                UserName = codigo,
                CodigoEstudiante = codigo,
                NombreCompleto = "Estudiante de prueba",
                Email = "estudiante@prueba.local",
                Activo = true
            };

            var creacion = await usuarios.CreateAsync(usuario, contrasenaCorrecta);
            Assert.IsTrue(
                creacion.Succeeded,
                string.Join("; ", creacion.Errors.Select(error => error.Description)));

            // Ejecución: se intenta ingresar con una contraseña incorrecta.
            var http = new DefaultHttpContext
            {
                RequestServices = alcance.ServiceProvider
            };
            alcance.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>()
                .HttpContext = http;

            var accion = new ActionContext(
                http,
                new RouteData(),
                new ControllerActionDescriptor());

            var controlador = new AccountController(
                alcance.ServiceProvider.GetRequiredService<SignInManager<Usuario>>(),
                usuarios)
            {
                ControllerContext = new ControllerContext(accion),
                Url = new UrlHelper(accion)
            };

            var resultado = await controlador.Login(new LoginViewModel
            {
                Identificador = codigo,
                Password = "ClaveIncorrecta123!"
            });

            // Comprobación: se mantiene en la vista de acceso y muestra el error.
            Assert.IsInstanceOfType(resultado, typeof(ViewResult));

            var mensajes = controlador.ModelState.Values
                .SelectMany(estado => estado.Errors)
                .Select(error => error.ErrorMessage);

            Assert.IsTrue(mensajes.Contains("Credenciales inválidas"));
        }

        // CASO DE PRUEBA (CP-03): Bloqueo tras tres intentos fallidos consecutivos
        [TestMethod]
        public async Task CP03_TresIntentosFallidos_BloqueaLaCuenta()
        {
            // Preparación: Identity se configura con el mismo límite de intentos que LabTrack.
            var servicios = new ServiceCollection();
            servicios.AddLogging();
            servicios.AddDataProtection();
            servicios.AddHttpContextAccessor();
            servicios.AddControllersWithViews();
            servicios.AddDbContext<LabTrackDbContext>(opciones =>
                opciones.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            servicios.AddIdentity<Usuario, IdentityRole>(opciones =>
            {
                opciones.Lockout.MaxFailedAccessAttempts = 3;
                opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                opciones.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<LabTrackDbContext>()
            .AddDefaultTokenProviders();

            using var proveedor = servicios.BuildServiceProvider();
            using var alcance = proveedor.CreateScope();

            var contexto = alcance.ServiceProvider.GetRequiredService<LabTrackDbContext>();
            await contexto.Database.EnsureCreatedAsync();

            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            const string codigo = "C00123456";
            const string contrasenaCorrecta = "Prueba123!";

            var usuario = new Usuario
            {
                UserName = codigo,
                CodigoEstudiante = codigo,
                NombreCompleto = "Estudiante de prueba",
                Email = "estudiante@prueba.local",
                Activo = true,
                LockoutEnabled = true
            };

            var creacion = await usuarios.CreateAsync(usuario, contrasenaCorrecta);
            Assert.IsTrue(
                creacion.Succeeded,
                string.Join("; ", creacion.Errors.Select(error => error.Description)));

            // Preparación de la petición al controlador.
            var http = new DefaultHttpContext
            {
                RequestServices = alcance.ServiceProvider
            };
            alcance.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>()
                .HttpContext = http;

            var accion = new ActionContext(
                http,
                new RouteData(),
                new ControllerActionDescriptor());

            var controlador = new AccountController(
                alcance.ServiceProvider.GetRequiredService<SignInManager<Usuario>>(),
                usuarios)
            {
                ControllerContext = new ControllerContext(accion),
                Url = new UrlHelper(accion)
            };

            // Ejecución: se ingresan credenciales incorrectas tres veces seguidas.
            for (var intento = 0; intento < 3; intento++)
            {
                controlador.ModelState.Clear();

                await controlador.Login(new LoginViewModel
                {
                    Identificador = codigo,
                    Password = "ClaveIncorrecta123!"
                });
            }

            // Comprobación del bloqueo y de la alerta mostrada.
            var mensajes = controlador.ModelState.Values
                .SelectMany(estado => estado.Errors)
                .Select(error => error.ErrorMessage);

            Assert.IsTrue(mensajes.Any(mensaje => mensaje.Contains("Alerta de seguridad")));
            Assert.IsTrue(await usuarios.IsLockedOutAsync(usuario));
        }
    }
}