using Hotel.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Hotel.Web.Repositorios;

public class ReservaRepositorio
{
    private readonly HotelContext _context;

    public ReservaRepositorio(HotelContext context)
    {
        _context = context;
    }

    // Lista todas las reservas con huésped y habitación (con su tipo)
    public async Task<List<Reserva>> ListarAsync()
    {
        return await _context.Reservas
            .AsNoTracking()
            .Include(r => r.IdHuespedNavigation)
            .Include(r => r.IdHabitacionNavigation)
                .ThenInclude(h => h.IdTipoHabitacionNavigation)
            .OrderByDescending(r => r.FechaRegistro)
            .ToListAsync();
    }

    public async Task<Reserva?> ObtenerAsync(int id)
    {
        return await _context.Reservas
            .AsNoTracking()
            .Include(r => r.IdHuespedNavigation)
            .Include(r => r.IdHabitacionNavigation)
                .ThenInclude(h => h.IdTipoHabitacionNavigation)
            .FirstOrDefaultAsync(r => r.IdReserva == id);
    }

    // Habitaciones activas disponibles en el rango ingreso–salida.
    // Una reserva BLOQUEA si: Estado != "Cancelada" && FechaIngreso < salida && FechaSalida > ingreso
    public async Task<List<Habitacion>> ListarDisponiblesAsync(DateOnly ingreso, DateOnly salida)
    {
        return await _context.Habitacions
            .AsNoTracking()
            .Include(h => h.IdTipoHabitacionNavigation)
            .Where(h => h.Activo && !h.Reservas.Any(r =>
                r.Estado != "Cancelada" &&
                r.FechaIngreso < salida &&
                r.FechaSalida > ingreso))
            .OrderBy(h => h.Numero)
            .ToListAsync();
    }

    // Verifica si una habitación está ocupada en el rango (para validar Create)
    public async Task<bool> HabitacionOcupadaAsync(int idHabitacion, DateOnly ingreso, DateOnly salida, int? excluirIdReserva = null)
    {
        return await _context.Reservas
            .AnyAsync(r =>
                r.IdHabitacion == idHabitacion &&
                r.IdReserva != (excluirIdReserva ?? 0) &&
                r.Estado != "Cancelada" &&
                r.FechaIngreso < salida &&
                r.FechaSalida > ingreso);
    }

    // Huéspedes activos para la lista desplegable
    public async Task<List<Huesped>> ObtenerHuespedesParaListaAsync()
    {
        return await _context.Huespeds
            .AsNoTracking()
            .Where(h => h.Activo)
            .OrderBy(h => h.Apellidos)
            .ThenBy(h => h.Nombres)
            .ToListAsync();
    }

    // Habitaciones activas para la lista desplegable
    public async Task<List<Habitacion>> ObtenerHabitacionesActivasParaListaAsync()
    {
        return await _context.Habitacions
            .AsNoTracking()
            .Include(h => h.IdTipoHabitacionNavigation)
            .Where(h => h.Activo)
            .OrderBy(h => h.Numero)
            .ToListAsync();
    }

    public async Task CrearAsync(Reserva reserva)
    {
        reserva.Estado = "Pendiente";
        reserva.FechaRegistro = DateTime.Now;
        _context.Reservas.Add(reserva);
        await _context.SaveChangesAsync();
    }

    // Cancela si está en estado Pendiente o Confirmada
    public async Task<bool> CancelarAsync(int id)
    {
        var reserva = await _context.Reservas.FindAsync(id);
        if (reserva == null) return false;
        if (reserva.Estado != "Pendiente" && reserva.Estado != "Confirmada") return false;

        reserva.Estado = "Cancelada";
        await _context.SaveChangesAsync();
        return true;
    }
}
