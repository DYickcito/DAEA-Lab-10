using Hotel.Web.Models;
using Hotel.Web.Repositorios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Hotel.Web.Controllers;

public class ReservasController : Controller
{
    private readonly ReservaRepositorio _reservas;

    public ReservasController(ReservaRepositorio reservas)
    {
        _reservas = reservas;
    }

    // Index: lista todas las reservas con opción de cancelar
    public async Task<IActionResult> Index()
    {
        return View(await _reservas.ListarAsync());
    }

    // Disponibles: filtro GET para buscar habitaciones disponibles en un rango de fechas
    public async Task<IActionResult> Disponibles(DateOnly? ingreso, DateOnly? salida)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        List<Habitacion>? habitaciones = null;
        int noches = 0;

        if (ingreso.HasValue && salida.HasValue)
        {
            if (ingreso.Value < hoy)
                ModelState.AddModelError(nameof(ingreso), "La fecha de ingreso no puede ser anterior a hoy.");
            else if (salida.Value <= ingreso.Value)
                ModelState.AddModelError(nameof(salida), "La fecha de salida debe ser posterior a la fecha de ingreso.");
            else
            {
                noches = salida.Value.DayNumber - ingreso.Value.DayNumber;
                habitaciones = await _reservas.ListarDisponiblesAsync(ingreso.Value, salida.Value);
            }
        }

        ViewBag.Ingreso = ingreso;
        ViewBag.Salida = salida;
        ViewBag.Noches = noches;
        return View(habitaciones);
    }

    // Create GET: formulario de nueva reserva (acepta datos preseleccionados desde Disponibles)
    public async Task<IActionResult> Create(int? idHabitacion, DateOnly? ingreso, DateOnly? salida)
    {
        await CargarListasAsync(idHabitacion);
        var reserva = new Reserva
        {
            IdHabitacion = idHabitacion ?? 0,
            FechaIngreso = ingreso ?? DateOnly.FromDateTime(DateTime.Today),
            FechaSalida = salida ?? DateOnly.FromDateTime(DateTime.Today.AddDays(1))
        };
        return View(reserva);
    }

    // Create POST: valida y persiste la reserva
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("IdHuesped,IdHabitacion,FechaIngreso,FechaSalida,Observaciones")] Reserva reserva)
    {
        ModelState.Remove(nameof(Reserva.IdHuespedNavigation));
        ModelState.Remove(nameof(Reserva.IdHabitacionNavigation));

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        if (reserva.FechaIngreso < hoy)
            ModelState.AddModelError(nameof(Reserva.FechaIngreso), "La fecha de ingreso no puede ser anterior a hoy.");

        if (reserva.FechaSalida <= reserva.FechaIngreso)
            ModelState.AddModelError(nameof(Reserva.FechaSalida), "La fecha de salida debe ser posterior al ingreso.");

        if (reserva.IdHabitacion > 0 && reserva.FechaSalida > reserva.FechaIngreso)
        {
            if (await _reservas.HabitacionOcupadaAsync(reserva.IdHabitacion, reserva.FechaIngreso, reserva.FechaSalida))
                ModelState.AddModelError(string.Empty, "La habitación seleccionada no está disponible para ese rango de fechas.");
        }

        if (!ModelState.IsValid)
        {
            await CargarListasAsync(reserva.IdHabitacion);
            return View(reserva);
        }

        await _reservas.CrearAsync(reserva);
        TempData["Mensaje"] = $"Reserva registrada correctamente con estado Pendiente.";
        return RedirectToAction(nameof(Index));
    }

    // Cancelar POST: cambia el estado a Cancelada (PRG)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id)
    {
        var ok = await _reservas.CancelarAsync(id);
        TempData["Mensaje"] = ok
            ? "Reserva cancelada correctamente."
            : "No se pudo cancelar la reserva (verifique su estado actual).";
        return RedirectToAction(nameof(Index));
    }

    private async Task CargarListasAsync(int? idHabitacionSel = null)
    {
        var huespedes = await _reservas.ObtenerHuespedesParaListaAsync();
        var habitaciones = await _reservas.ObtenerHabitacionesActivasParaListaAsync();

        ViewBag.Huespedes = new SelectList(
            huespedes,
            nameof(Huesped.IdHuesped),
            "Nombre",   // generamos la propiedad Nombre en la vista con DisplayMember
            null);

        // Para el select mostramos Número + Tipo
        ViewBag.HuespedesList = huespedes;
        ViewBag.HabitacionesList = habitaciones;
        ViewBag.Habitaciones = new SelectList(
            habitaciones.Select(h => new
            {
                h.IdHabitacion,
                Label = $"Hab. {h.Numero} - {h.IdTipoHabitacionNavigation.Nombre} (S/ {h.IdTipoHabitacionNavigation.PrecioNoche:N2}/noche)"
            }),
            "IdHabitacion",
            "Label",
            idHabitacionSel);
    }
}
