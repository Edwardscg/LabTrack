using LabTrack.Controllers;
using LabTrack.Data;
using LabTrack.Models;
using LabTrack.Services;
using LabTrack.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabTrack.Tests
{
    [TestClass]
    public class EquiposControllerTest
    {
        private static LabTrackDbContext CrearContexto()
        {
            var opciones = new DbContextOptionsBuilder<LabTrackDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var contexto = new LabTrackDbContext(opciones);
            contexto.Database.EnsureCreated();
            return contexto;
        }

        // CASO DE PRUEBA (CP-04): Visualizar los datos de los equipos
        [TestMethod]
        public async Task CP04_VisualizarDatosEquipos_MuestraNombreCodigoCategoriaYEstado()
        {
            // Preparación
            using var contexto = CrearContexto();
            var reloj = new LabClock();
            var ahora = reloj.Now;

            var categoria = new Categoria
            {
                Id = 1,
                Nombre = "Cómputo"
            };

            var estudiante = new Usuario
            {
                Id = "estudiante-prueba",
                UserName = "C00123456",
                CodigoEstudiante = "C00123456",
                NombreCompleto = "Estudiante de prueba"
            };

            var encargado = new Usuario
            {
                Id = "encargado-prueba",
                UserName = "encargado",
                NombreCompleto = "Encargado de prueba"
            };

            var disponible = new Equipo
            {
                Id = 1,
                CodigoInventario = "CMP-001",
                Nombre = "Laptop disponible",
                Descripcion = "Equipo disponible para prácticas.",
                CategoriaId = categoria.Id,
                Categoria = categoria
            };

            var prestado = new Equipo
            {
                Id = 2,
                CodigoInventario = "CMP-002",
                Nombre = "Laptop prestada",
                Descripcion = "Equipo reservado actualmente.",
                CategoriaId = categoria.Id,
                Categoria = categoria
            };

            contexto.Categorias.Add(categoria);
            contexto.Users.AddRange(estudiante, encargado);
            contexto.Equipos.AddRange(disponible, prestado);
            contexto.Reservas.Add(new Reserva
            {
                EstudianteId = estudiante.Id,
                Estudiante = estudiante,
                EquipoId = prestado.Id,
                Equipo = prestado,
                Inicio = ahora.AddMinutes(-30),
                Fin = ahora.AddMinutes(30),
                FechaSolicitud = ahora.AddHours(-1),
                Estado = EstadoReserva.Aprobada,
                EncargadoId = encargado.Id,
                Encargado = encargado,
                FechaDecision = ahora.AddMinutes(-45)
            });

            await contexto.SaveChangesAsync();

            // Ejecución
            var controlador = new EquiposController(contexto, reloj);
            var resultado = await controlador.Index(null);

            // Comprobación
            Assert.IsInstanceOfType(resultado, typeof(ViewResult));

            var modelo = (CatalogoViewModel)((ViewResult)resultado).Model!;
            Assert.AreEqual(2, modelo.Equipos.Count);

            var equipoDisponible = modelo.Equipos.Single(equipo => equipo.Codigo == "CMP-001");
            Assert.AreEqual("Laptop disponible", equipoDisponible.Nombre);
            Assert.AreEqual("Cómputo", equipoDisponible.Categoria);
            Assert.IsFalse(equipoDisponible.Prestado);

            var equipoPrestado = modelo.Equipos.Single(equipo => equipo.Codigo == "CMP-002");
            Assert.AreEqual("Laptop prestada", equipoPrestado.Nombre);
            Assert.AreEqual("Cómputo", equipoPrestado.Categoria);
            Assert.IsTrue(equipoPrestado.Prestado);
        }

        // CASO DE PRUEBA (CP-05): Filtrar equipos por categoría
        [TestMethod]
        public async Task CP05_FiltrarEquiposPorCategoria_MuestraSoloLaCategoriaSeleccionada()
        {
            // Preparación
            using var contexto = CrearContexto();
            var reloj = new LabClock();

            var computo = new Categoria { Id = 1, Nombre = "Cómputo" };
            var electronica = new Categoria { Id = 2, Nombre = "Electrónica" };
            var redes = new Categoria { Id = 3, Nombre = "Redes" };

            contexto.Categorias.AddRange(computo, electronica, redes);
            contexto.Equipos.AddRange(
                new Equipo
                {
                    Id = 1,
                    CodigoInventario = "CMP-001",
                    Nombre = "Laptop",
                    Descripcion = "Equipo de cómputo.",
                    CategoriaId = computo.Id,
                    Categoria = computo
                },
                new Equipo
                {
                    Id = 2,
                    CodigoInventario = "ELE-001",
                    Nombre = "Osciloscopio",
                    Descripcion = "Equipo de electrónica.",
                    CategoriaId = electronica.Id,
                    Categoria = electronica
                },
                new Equipo
                {
                    Id = 3,
                    CodigoInventario = "RED-001",
                    Nombre = "Router",
                    Descripcion = "Equipo de redes.",
                    CategoriaId = redes.Id,
                    Categoria = redes
                });

            await contexto.SaveChangesAsync();

            var controlador = new EquiposController(contexto, reloj);
            var categorias = new[] { computo, electronica, redes };

            // Ejecución y comprobación: se filtra por cada una de las tres categorías.
            foreach (var categoria in categorias)
            {
                var resultado = await controlador.Index(categoria.Id);

                Assert.IsInstanceOfType(resultado, typeof(ViewResult));

                var modelo = (CatalogoViewModel)((ViewResult)resultado).Model!;
                Assert.AreEqual(categoria.Id, modelo.CategoriaId);
                Assert.AreEqual(1, modelo.Total);
                Assert.AreEqual(1, modelo.Equipos.Count);
                Assert.AreEqual(categoria.Nombre, modelo.Equipos.Single().Categoria);
            }
        }

        // CASO DE PRUEBA (CP-06): Filtrar una categoría sin equipos disponibles
        [TestMethod]
        public async Task CP06_FiltrarCategoriaSinEquipos_DevuelveCatalogoVacio()
        {
            // Preparación
            using var contexto = CrearContexto();
            var reloj = new LabClock();

            var categoriaVacia = new Categoria
            {
                Id = 1,
                Nombre = "Redes"
            };

            var otraCategoria = new Categoria
            {
                Id = 2,
                Nombre = "Cómputo"
            };

            contexto.Categorias.AddRange(categoriaVacia, otraCategoria);
            contexto.Equipos.Add(new Equipo
            {
                Id = 1,
                CodigoInventario = "CMP-001",
                Nombre = "Laptop",
                Descripcion = "Equipo de cómputo.",
                CategoriaId = otraCategoria.Id,
                Categoria = otraCategoria
            });

            await contexto.SaveChangesAsync();

            // Ejecución
            var controlador = new EquiposController(contexto, reloj);
            var resultado = await controlador.Index(categoriaVacia.Id);

            // Comprobación
            Assert.IsInstanceOfType(resultado, typeof(ViewResult));

            var modelo = (CatalogoViewModel)((ViewResult)resultado).Model!;
            Assert.AreEqual(categoriaVacia.Id, modelo.CategoriaId);
            Assert.AreEqual(0, modelo.Total);
            Assert.AreEqual(0, modelo.Equipos.Count);
        }
    }
}