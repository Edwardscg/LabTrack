using LabTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LabTrack.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        var db = services.GetRequiredService<LabTrackDbContext>();
        await db.Database.MigrateAsync();
        var users = services.GetRequiredService<UserManager<Usuario>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Estudiante, Roles.Encargado })
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new IdentityRole(role)));

        await AddUserAsync(users, configuration, "Encargado", "encargado", "Encargado de laboratorio", null, Roles.Encargado);
        await AddUserAsync(users, configuration, "Estudiante1", "C00123456", "Ana Torres", "C00123456", Roles.Estudiante);
        await AddUserAsync(users, configuration, "Estudiante2", "C00654321", "Diego Ramos", "C00654321", Roles.Estudiante);

        foreach (var name in new[] { "Cómputo", "Electrónica", "Redes" })
            if (!await db.Categorias.AnyAsync(c => c.Nombre == name)) db.Categorias.Add(new Categoria { Nombre = name });
        await db.SaveChangesAsync();
        var categories = await db.Categorias.ToDictionaryAsync(c => c.Nombre, c => c.Id);
        var equipment = new[]
        {
            ("CMP-001", "Laptop de desarrollo", "Cómputo", "Estación portátil para programación y prácticas de software."),
            ("CMP-002", "Estación de trabajo", "Cómputo", "Equipo de escritorio para diseño, simulación y análisis."),
            ("ELE-001", "Osciloscopio digital", "Electrónica", "Medición y visualización de señales en circuitos electrónicos."),
            ("ELE-002", "Kit de microcontroladores", "Electrónica", "Componentes para prototipado y programación de sistemas embebidos."),
            ("RED-001", "Router de laboratorio", "Redes", "Configuración de enrutamiento y prácticas de conectividad."),
            ("RED-002", "Switch administrable", "Redes", "Prácticas de conmutación, segmentación y redes locales.")
        };
        foreach (var (code, name, category, description) in equipment)
            if (!await db.Equipos.AnyAsync(e => e.CodigoInventario == code))
                db.Equipos.Add(new Equipo { CodigoInventario = code, Nombre = name, CategoriaId = categories[category], Descripcion = description });
        await db.SaveChangesAsync();
    }

    private static async Task AddUserAsync(UserManager<Usuario> users, IConfiguration configuration,
        string key, string login, string name, string? code, string role)
    {
        var user = await users.FindByNameAsync(login);
        if (user is null)
        {
            var password = configuration[$"Seed:{key}Password"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException($"Configure Seed:{key}Password antes de inicializar las cuentas.");
            user = new Usuario { UserName = login, NombreCompleto = name, CodigoEstudiante = code, LockoutEnabled = true };
            Ensure(await users.CreateAsync(user, password));
        }
        if (!await users.IsInRoleAsync(user, role)) Ensure(await users.AddToRoleAsync(user, role));
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
