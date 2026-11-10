using LabTrack.Data;
using LabTrack.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LabTrack.Services;

public record ResultadoReserva(bool Exito, string Mensaje);

public class ReservaService(LabTrackDbContext db, LabClock clock)
{
    public Task<ResultadoReserva> SolicitarAsync(string studentId, int equipmentId, DateTime start, DateTime end) =>
        MutateAsync(async () =>
        {
            var now = clock.Now;
            if (start < now || end <= start || start.Date != end.Date)
                return new(false, "Selecciona un horario futuro válido, con inicio y fin en el mismo día.");
            if (!await db.Users.AnyAsync(u => u.Id == studentId && u.Activo && u.CodigoEstudiante != null))
                return new(false, "La cuenta de estudiante no está habilitada.");
            if (!await db.Equipos.AnyAsync(e => e.Id == equipmentId))
                return new(false, "El equipo seleccionado no existe.");
            if (await StudentConflictAsync(studentId, start, end))
                return new(false, "Ya tienes una solicitud pendiente o aprobada en ese horario.");
            if (await EquipmentConflictAsync(equipmentId, start, end))
                return new(false, "El equipo ya tiene una reserva aprobada que se cruza con ese horario.");
            db.Reservas.Add(new Reserva
            {
                EstudianteId = studentId, EquipoId = equipmentId,
                Inicio = start, Fin = end, FechaSolicitud = now
            });
            await db.SaveChangesAsync();
            return new(true, "Solicitud enviada. El encargado revisará tu reserva.");
        });

    private Task<bool> StudentConflictAsync(string id, DateTime start, DateTime end, int excluding = 0) =>
        db.Reservas.AnyAsync(r => r.Id != excluding && r.EstudianteId == id &&
            (r.Estado == EstadoReserva.Pendiente || r.Estado == EstadoReserva.Aprobada) && start < r.Fin && end > r.Inicio);

    private Task<bool> EquipmentConflictAsync(int id, DateTime start, DateTime end, int excluding = 0) =>
        db.Reservas.AnyAsync(r => r.Id != excluding && r.EquipoId == id &&
            r.Estado == EstadoReserva.Aprobada && start < r.Fin && end > r.Inicio);

    private async Task<ResultadoReserva> MutateAsync(Func<Task<ResultadoReserva>> mutation)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            // Todas las escrituras de reservas comparten este bloqueo transaccional en SQL Server.
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = N'LabTrack.Reservas',
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @result < 0 THROW 51001, 'No se pudo adquirir el bloqueo de reservas.', 1;
                """);
            var result = await mutation();
            if (result.Exito) await transaction.CommitAsync();
            else await transaction.RollbackAsync();
            return result;
        }
        catch (SqlException ex) when (ex.Number is 1205 or 51001)
        {
            await transaction.RollbackAsync();
            return new(false, "Otra operación está actualizando las reservas. Inténtalo de nuevo.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return new(false, "La reserva cambió mientras se procesaba. Actualiza la página e inténtalo de nuevo.");
        }
    }
}
