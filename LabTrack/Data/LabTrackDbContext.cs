using LabTrack.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LabTrack.Data;

public class LabTrackDbContext(DbContextOptions<LabTrackDbContext> options)
    : IdentityDbContext<Usuario>(options)
{
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<Reserva> Reservas => Set<Reserva>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Usuario>(e =>
        {
            e.HasIndex(u => u.CodigoEstudiante).IsUnique().HasFilter("[CodigoEstudiante] IS NOT NULL");
            e.ToTable("AspNetUsers", t => t.HasCheckConstraint("CK_Usuario_Codigo",
                "[CodigoEstudiante] IS NULL OR (LEN([CodigoEstudiante]) = 9 AND [CodigoEstudiante] COLLATE Latin1_General_BIN2 LIKE 'C00[0-9][0-9][0-9][0-9][0-9][0-9]' AND [UserName] = [CodigoEstudiante])"));
        });
        builder.Entity<Categoria>().HasIndex(c => c.Nombre).IsUnique();
        builder.Entity<Equipo>().HasIndex(e => e.CodigoInventario).IsUnique();
        builder.Entity<Equipo>().HasOne(e => e.Categoria).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Reserva>(e =>
        {
            e.HasOne(r => r.Estudiante).WithMany().HasForeignKey(r => r.EstudianteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Encargado).WithMany().HasForeignKey(r => r.EncargadoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Equipo).WithMany(q => q.Reservas).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.EquipoId, r.Estado, r.Inicio, r.Fin });
            e.HasIndex(r => new { r.EstudianteId, r.Estado, r.Inicio, r.Fin });
            e.ToTable("Reservas", t =>
            {
                t.HasCheckConstraint("CK_Reserva_Horario", "[Fin] > [Inicio]");
                t.HasCheckConstraint("CK_Reserva_Estado", "[Estado] BETWEEN 0 AND 3");
                t.HasCheckConstraint("CK_Reserva_Motivo", "[Estado] <> 2 OR LEN(LTRIM(RTRIM([MotivoRechazo]))) > 0 AND [MotivoRechazo] IS NOT NULL");
                t.HasCheckConstraint("CK_Reserva_Devolucion", "([Estado] = 3 AND [FechaDevolucion] IS NOT NULL AND [FechaDevolucion] >= [Inicio]) OR ([Estado] <> 3 AND [FechaDevolucion] IS NULL)");
                t.HasCheckConstraint("CK_Reserva_Decision", "[Estado] = 0 OR ([EncargadoId] IS NOT NULL AND [FechaDecision] IS NOT NULL)");
            });
        });
    }
}
