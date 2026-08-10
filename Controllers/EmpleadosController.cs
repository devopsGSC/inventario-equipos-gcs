using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class EmpleadosController : BaseController
{
    private readonly AppDbContext _db;
    private readonly PdfService _pdf;
    private readonly UserManager<UsuarioApp> _users;
    public EmpleadosController(AppDbContext db, PdfService pdf, UserManager<UsuarioApp> users, PermisoService permisos) : base(permisos)
    { _db = db; _pdf = pdf; _users = users; }

    public async Task<IActionResult> Index(string? departamento, bool? activo, string? buscar, int pagina = 1)
    {
        if (!await Puede("empleados.ver")) return AccesoDenegado();

        const int tamPagina = 12;
        var query = _db.Empleados.Include(e => e.Departamento).AsQueryable();

        if (!string.IsNullOrEmpty(departamento))
            query = query.Where(e => e.Departamento!.Nombre == departamento);
        if (activo.HasValue)
            query = query.Where(e => e.Activo == activo.Value);
        if (!string.IsNullOrEmpty(buscar))
            query = query.Where(e => e.Nombre.Contains(buscar) ||
                                     e.CodigoEmpleado.Contains(buscar) ||
                                     e.DUI.Contains(buscar));

        var total = await query.CountAsync();
        var empleados = await query
            .OrderByDescending(e => e.CodigoEmpleado)
            .Skip((pagina - 1) * tamPagina)
            .Take(tamPagina)
            .ToListAsync();

        ViewBag.Departamento = departamento;
        ViewBag.Activo = activo;
        ViewBag.Buscar = buscar;
        ViewBag.Departamentos = await _db.Departamentos
            .OrderBy(d => d.Nombre)
            .Select(d => d.Nombre)
            .ToListAsync();
        ViewBag.Paginacion = new PaginacionViewModel
        {
            PaginaActual   = pagina,
            TotalPaginas   = (int)Math.Ceiling(total / (double)tamPagina),
            TotalRegistros = total,
            TamañoPagina   = tamPagina
        };
        return View(empleados);
    }

    public async Task<IActionResult> Details(int id, int pagina = 1)
    {
        if (!await Puede("empleados.detalle")) return AccesoDenegado();

        const int tamPagina = 8;
        var empleado = await _db.Empleados
            .Include(e => e.Departamento)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (empleado == null) return NotFound();

        var equiposActuales = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => m.EmpleadoId == id && m.FechaDevolucion == null &&
                        (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
            .ToListAsync();
        ViewBag.EquiposActuales = equiposActuales;

        ViewBag.PerifsActuales = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Equipo)
            .Where(ep => ep.EmpleadoId == id && ep.FechaDesvinculacion == null)
            .OrderByDescending(ep => ep.FechaAsignacion)
            .ToListAsync();

        ViewBag.HistorialPerifericos = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Equipo)
            .Where(ep => ep.EmpleadoId == id && ep.TipoMovimiento != "Devolucion")
            .OrderByDescending(ep => ep.FechaAsignacion)
            .ToListAsync();

        ViewBag.LicenciasActuales = await _db.LicenciasAsignaciones
            .Include(la => la.TipoLicencia)
            .Include(la => la.Equipo)
            .Where(la => la.EmpleadoId == id && la.FechaDesvinculacion == null)
            .OrderByDescending(la => la.FechaAsignacion)
            .ToListAsync();

        ViewBag.HistorialLicencias = await _db.LicenciasAsignaciones
            .Include(la => la.TipoLicencia)
            .Include(la => la.Equipo)
            .Where(la => la.EmpleadoId == id && la.TipoMovimiento != "Devolucion")
            .OrderByDescending(la => la.FechaAsignacion)
            .ToListAsync();

        var totalHistorial = await _db.Movimientos.CountAsync(m => m.EmpleadoId == id);
        ViewBag.Historial = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => m.EmpleadoId == id)
            .OrderByDescending(m => m.FechaInicio)
            .Skip((pagina - 1) * tamPagina).Take(tamPagina)
            .ToListAsync();
        ViewBag.Paginacion = new InventarioTI.ViewModels.PaginacionViewModel
        {
            PaginaActual   = pagina,
            TotalPaginas   = (int)Math.Ceiling(totalHistorial / (double)tamPagina),
            TotalRegistros = totalHistorial,
            TamañoPagina   = tamPagina
        };
        return View(empleado);
    }

    // Carta con TODOS los equipos y periféricos que tiene actualmente el
    // empleado, para cuando ya se firmó cada asignación por separado pero
    // se necesita un solo documento con el resumen completo.
    public async Task<IActionResult> DescargarCartaGeneral(int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var empleado = await _db.Empleados.Include(e => e.Departamento).FirstOrDefaultAsync(e => e.Id == id);
        if (empleado == null) return NotFound();

        var equiposActuales = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => m.EmpleadoId == id && m.FechaDevolucion == null &&
                        (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
            .ToListAsync();

        var perifsActuales = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Equipo)
            .Where(ep => ep.EmpleadoId == id && ep.FechaDesvinculacion == null)
            .OrderByDescending(ep => ep.FechaAsignacion)
            .ToListAsync();

        var usuarioActual = await _users.GetUserAsync(User);

        var data = new CartaGeneralData
        {
            Colaborador    = empleado.Nombre,
            Centro         = empleado.Departamento?.Nombre ?? "",
            Area           = empleado.Cargo ?? "",
            CodEmpleado    = empleado.CodigoEmpleado,
            Identificacion = empleado.DUI ?? "",
            NombreEmisor   = usuarioActual?.NombreCompleto,
            RutaFirmaIT    = usuarioActual?.RutaFirmaIT,
            Equipos = equiposActuales.Select(m => new EquipoResumenItem
            {
                Tipo        = m.Equipo?.TipoEquipo?.Nombre ?? "",
                Marca       = m.Equipo?.Marca ?? "",
                Modelo      = m.Equipo?.Modelo ?? "",
                NumeroSerie = m.Equipo?.NumeroSerie ?? "",
                Movimiento  = m.TipoMovimiento,
                Desde       = m.FechaInicio.ToString("dd/MM/yyyy")
            }).ToList(),
            Perifericos = perifsActuales.Select(ep => new PerifericoResumenItem
            {
                Tipo        = ep.Periferico?.TipoPeriferico?.Nombre ?? "",
                Marca       = ep.Periferico?.Marca ?? "",
                Modelo      = ep.Periferico?.Modelo ?? "",
                NumeroSerie = ep.Periferico?.NumeroSerie ?? "",
                ViaEquipo   = ep.Equipo?.NombreEquipo ?? "Directo"
            }).ToList()
        };

        var bytes = _pdf.GenerarCartaGeneral(data);
        var nombre = $"Carta_General_{SanitizarNombreArchivo(empleado.Nombre)}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    private static string SanitizarNombreArchivo(string nombre) =>
        string.Join("_", nombre.Split(Path.GetInvalidFileNameChars().Append(' ').ToArray(), StringSplitOptions.RemoveEmptyEntries));

    public async Task<IActionResult> Create()
    {
        if (!await Puede("empleados.crear")) return AccesoDenegado();

        ViewBag.Departamentos = new SelectList(await _db.Departamentos.ToListAsync(), "Id", "Nombre");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Empleado empleado)
    {
        if (!await Puede("empleados.crear")) return AccesoDenegado();

        if (await _db.Empleados.AnyAsync(e => e.CodigoEmpleado == empleado.CodigoEmpleado))
            ModelState.AddModelError("CodigoEmpleado", "Ya existe un empleado con ese código.");
        if (await _db.Empleados.AnyAsync(e => e.DUI == empleado.DUI))
            ModelState.AddModelError("DUI", "Ya existe un empleado con ese DUI.");

        if (!ModelState.IsValid)
        {
            ViewBag.Departamentos = new SelectList(await _db.Departamentos.ToListAsync(), "Id", "Nombre");
            return View(empleado);
        }
        empleado.FechaRegistro = DateTime.Now;
        empleado.CreadoPorUsuarioId = UsuarioActualId;
        _db.Empleados.Add(empleado);
        await _db.SaveChangesAsync();
        TempData["OK"] = "Empleado registrado correctamente.";
        return RedirectToAction(nameof(Details), new { id = empleado.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        if (!await Puede("empleados.editar")) return AccesoDenegado();

        var empleado = await _db.Empleados.FindAsync(id);
        if (empleado == null) return NotFound();
        ViewBag.Departamentos = new SelectList(await _db.Departamentos.ToListAsync(), "Id", "Nombre", empleado.DepartamentoId);
        return View(empleado);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Empleado empleado)
    {
        if (!await Puede("empleados.editar")) return AccesoDenegado();

        if (id != empleado.Id) return BadRequest();
        if (await _db.Empleados.AnyAsync(e => e.CodigoEmpleado == empleado.CodigoEmpleado && e.Id != id))
            ModelState.AddModelError("CodigoEmpleado", "Ya existe un empleado con ese código.");
        if (await _db.Empleados.AnyAsync(e => e.DUI == empleado.DUI && e.Id != id))
            ModelState.AddModelError("DUI", "Ya existe un empleado con ese DUI.");

        if (!ModelState.IsValid)
        {
            ViewBag.Departamentos = new SelectList(await _db.Departamentos.ToListAsync(), "Id", "Nombre", empleado.DepartamentoId);
            return View(empleado);
        }
        var original = await _db.Empleados.FindAsync(id);
        if (original == null) return NotFound();
        original.CodigoEmpleado = empleado.CodigoEmpleado;
        original.Nombre = empleado.Nombre;
        original.DUI = empleado.DUI;
        original.Cargo = empleado.Cargo;
        original.DepartamentoId = empleado.DepartamentoId;
        original.Activo = empleado.Activo;
        await _db.SaveChangesAsync();
        TempData["OK"] = "Empleado actualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
