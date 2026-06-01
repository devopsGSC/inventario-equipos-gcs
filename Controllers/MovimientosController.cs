using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class MovimientosController : Controller
{
    private readonly AppDbContext _db;
    private readonly PdfService _pdf;
    public MovimientosController(AppDbContext db, PdfService pdf)
    { _db = db; _pdf = pdf; }

    public async Task<IActionResult> Registrar(int equipoId)
    {
        var equipo = await _db.Equipos.Include(e => e.TipoEquipo).FirstOrDefaultAsync(e => e.Id == equipoId);
        if (equipo == null) return NotFound();
        if (equipo.Estado == "Baja")
        {
            TempData["Error"] = "No se pueden registrar movimientos en equipos dados de baja.";
            return RedirectToAction("Details", "Equipos", new { id = equipoId });
        }

        var vm = new MovimientoCreateViewModel
        {
            EquipoId = equipoId,
            Equipo = equipo,
            Empleados = await _db.Empleados.Where(e => e.Activo)
                .Include(e => e.Departamento)
                .OrderBy(e => e.Nombre).ToListAsync()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(MovimientoCreateViewModel vm)
    {
        var equipo = await _db.Equipos.FindAsync(vm.EquipoId);
        if (equipo == null) return NotFound();

        // Limpiar ModelState de campos complejos que no vienen del form
        ModelState.Remove("Equipo");
        ModelState.Remove("Empleados");
        ModelState.Remove("EmpleadoId");
        ModelState.Remove("FechaFinEstimada");
        ModelState.Remove("FechaInicio");

        if (vm.FechaInicio == default)
            vm.FechaInicio = DateTime.Now;

        bool requiereEmpleado = vm.TipoMovimiento == "Asignacion" || vm.TipoMovimiento == "Prestamo";

        // Validaciones manuales
        if (string.IsNullOrEmpty(vm.TipoMovimiento))
            ModelState.AddModelError("TipoMovimiento", "Seleccione un tipo de movimiento.");

        if (requiereEmpleado && vm.EmpleadoId == null)
            ModelState.AddModelError("EmpleadoId", "Debe seleccionar un empleado.");

        // FechaFinEstimada es opcional para préstamos

        if (!ModelState.IsValid)
        {
            vm.Equipo = await _db.Equipos.Include(e => e.TipoEquipo).FirstAsync(e => e.Id == vm.EquipoId);
            vm.Empleados = await _db.Empleados.Where(e => e.Activo)
                .Include(e => e.Departamento).OrderBy(e => e.Nombre).ToListAsync();
            return View(vm);
        }

        // Cerrar movimiento activo si es devolución o salida de garantía
        // *** Sin .Contains() en la query EF — comparaciones explícitas ***
        int? empleadoAnteriorId = null;
        if (vm.TipoMovimiento == "Devolucion" || vm.TipoMovimiento == "SalidaGarantia")
        {
            var activo = await _db.Movimientos.FirstOrDefaultAsync(m =>
                m.EquipoId == vm.EquipoId &&
                m.FechaDevolucion == null &&
                (m.TipoMovimiento == "Asignacion" ||
                 m.TipoMovimiento == "Prestamo" ||
                 m.TipoMovimiento == "EntradaGarantia"));

            if (activo != null)
            {
                activo.FechaDevolucion = DateTime.Now;
                empleadoAnteriorId = activo.EmpleadoId; // guardar para el finiquito
            }

            // Desvincular periféricos — regresan al stock disponible
            var perifsActivos = await _db.EquiposPerifericos
                .Include(ep => ep.Periferico)
                .Where(ep => ep.EquipoId == vm.EquipoId && ep.FechaDesvinculacion == null)
                .ToListAsync();
            foreach (var ep in perifsActivos)
            {
                ep.FechaDesvinculacion = DateTime.Now;
                if (ep.Periferico != null) ep.Periferico.Estado = "Disponible";
            }
        }

        // Actualizar estado del equipo
        equipo.Estado = vm.TipoMovimiento switch
        {
            "Asignacion"      => "Asignado",
            "Prestamo"        => "Prestamo",
            "EntradaGarantia" => "EnGarantia",
            "Devolucion"      => "Bodega",
            "SalidaGarantia"  => "Bodega",
            "Baja"            => "Baja",
            "Reactivacion"    => "Bodega",
            _                 => equipo.Estado
        };

        var movimiento = new Movimiento
        {
            EquipoId         = vm.EquipoId,
            EmpleadoId       = requiereEmpleado ? vm.EmpleadoId
                             : vm.TipoMovimiento == "Devolucion" ? empleadoAnteriorId
                             : null,
            TipoMovimiento   = vm.TipoMovimiento!,
            FechaInicio      = vm.FechaInicio,
            FechaFinEstimada = vm.FechaFinEstimada,
            Observaciones    = vm.Observaciones,
            FirmaEmpleado    = vm.FirmaEmpleado  // guardada en BD, no en TempData
        };
        _db.Movimientos.Add(movimiento);
        await _db.SaveChangesAsync();

        // Adjuntar periféricos si se seleccionaron (solo Asignacion y Prestamo)
        if (requiereEmpleado && !string.IsNullOrEmpty(Request.Form["perifericosIds"]))
        {
            var ids = Request.Form["perifericosIds"].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse);
            foreach (var pid in ids)
            {
                var periferico = await _db.Perifericos.FindAsync(pid);
                if (periferico != null && periferico.Estado == "Disponible")
                {
                    periferico.Estado = "Asignado";
                    _db.EquiposPerifericos.Add(new EquipoPeriferico
                    {
                        EquipoId = vm.EquipoId, PerifericoId = pid,
                        FechaAsignacion = DateTime.Now
                    });
                }
            }
            await _db.SaveChangesAsync();
        }

        TempData["OK"] = "Movimiento registrado correctamente.";

        // Carta de préstamo → va a Carta (descarga inmediata)
        if (vm.TipoMovimiento == "Prestamo")
            return RedirectToAction(nameof(Carta), new { id = movimiento.Id });

        // Carta de asignación → va a Carta (compromiso simple)
        if (vm.TipoMovimiento == "Asignacion")
            return RedirectToAction(nameof(Carta), new { id = movimiento.Id });

        // Devolución con empleado identificado → va al Finiquito TI
        if (vm.TipoMovimiento == "Devolucion" && movimiento.EmpleadoId != null)
            return RedirectToAction(nameof(Finiquito), new { movimientoId = movimiento.Id });
        return RedirectToAction("Details", "Equipos", new { id = vm.EquipoId });
    }

    public async Task<IActionResult> Carta(int id)
    {
        var movimiento = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(e => e!.TipoEquipo)
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movimiento == null) return NotFound();
        return View(movimiento);
    }

    public async Task<IActionResult> DescargarCarta(int id)
    {
        var movimiento = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(e => e!.TipoEquipo)
            .Include(m => m.Equipo).ThenInclude(e => e!.EquiposPerifericos.Where(ep => ep.FechaDesvinculacion == null))
                .ThenInclude(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movimiento == null || movimiento.Empleado == null) return NotFound();

        byte[] bytes = movimiento.TipoMovimiento == "Prestamo"
            ? _pdf.GenerarCartaPrestamo(movimiento, movimiento.FirmaEmpleado)
            : _pdf.GenerarCartaCompromiso(movimiento, movimiento.FirmaEmpleado);

        movimiento.CartaGenerada = true;
        await _db.SaveChangesAsync();

        var nombre = $"Carta_{movimiento.TipoMovimiento}_{movimiento.Empleado.CodigoEmpleado}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    public async Task<IActionResult> Finiquito(int movimientoId)
    {
        var mov = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(e => e!.TipoEquipo)
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .FirstOrDefaultAsync(m => m.Id == movimientoId);
        if (mov == null) return NotFound();

        // Buscar el movimiento anterior (asignación/préstamo) para precargar datos
        var movAnterior = await _db.Movimientos
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .Where(m => m.EquipoId == mov.EquipoId &&
                        m.Id != movimientoId &&
                        (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
            .OrderByDescending(m => m.FechaInicio)
            .FirstOrDefaultAsync();

        // Periféricos que tenía asignados (recién desvinculados)
        var perifsDevueltos = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Where(ep => ep.EquipoId == mov.EquipoId &&
                         ep.FechaDesvinculacion.HasValue &&
                         ep.FechaDesvinculacion.Value.Date == DateTime.Today)
            .ToListAsync();

        ViewBag.MovAnterior      = movAnterior;
        ViewBag.PerifsDevueltos  = perifsDevueltos;
        return View(mov);
    }

    [HttpPost]
    public async Task<IActionResult> DescargarFiniquito(int movimientoId, string motivo,
        string receptorNombre, string receptorCentro,
        string ram, string disco, string procesador,
        string observaciones, string? firmaEmpleado)
    {
        var mov = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(e => e!.TipoEquipo)
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .FirstOrDefaultAsync(m => m.Id == movimientoId);
        if (mov == null || mov.Empleado == null) return NotFound();

        var eq  = mov.Equipo!;
        var emp = mov.Empleado;

        // Cargar periféricos devueltos hoy con este equipo
        var perifsDevueltos = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Where(ep => ep.EquipoId == eq.Id &&
                         ep.FechaDesvinculacion.HasValue &&
                         ep.FechaDesvinculacion.Value.Date == DateTime.Today)
            .ToListAsync();

        var d = new FiniquitoData
        {
            Fecha          = mov.FechaInicio.ToString("dd/MMM/yyyy"),
            Colaborador    = emp.Nombre,
            Centro         = emp.Departamento?.Nombre ?? "",
            Area           = emp.Cargo,
            CodEmpleado    = emp.CodigoEmpleado,
            Identificacion = emp.DUI,
            Tipo           = eq.TipoEquipo?.Nombre ?? "",
            Marca          = eq.Marca,
            Modelo         = eq.Modelo,
            ServiceTag     = eq.NumeroSerie,
            Ram            = ram ?? "",
            Disco          = disco ?? "",
            Procesador     = procesador ?? "",
            FechaGarantia  = eq.FechaGarantia?.ToString("dd/MM/yyyy") ?? "",
            Accesorio      = eq.Accesorios ?? "",
            Sku            = eq.NumeroSerie,
            Observaciones  = observaciones ?? "",
            Motivo         = motivo ?? "fin_laboral",
            ReceptorNombre = receptorNombre ?? "",
            ReceptorCentro = receptorCentro ?? "GCS Santa Elena",
            FirmaEmpleadoBase64 = !string.IsNullOrEmpty(firmaEmpleado) ? firmaEmpleado : mov.FirmaEmpleado ?? "",
            Perifericos    = perifsDevueltos.Select(ep => new PerifericoFiniquito
            {
                Tipo        = ep.Periferico?.TipoPeriferico?.Nombre ?? "",
                Marca       = ep.Periferico?.Marca ?? "",
                Modelo      = ep.Periferico?.Modelo ?? "",
                NumeroSerie = ep.Periferico?.NumeroSerie ?? ""
            }).ToList()
        };

        var bytes = _pdf.GenerarFiniquito(d);
        mov.CartaGenerada = true;
        await _db.SaveChangesAsync();

        var nombre = $"Finiquito_TI_{emp.CodigoEmpleado}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }
}
