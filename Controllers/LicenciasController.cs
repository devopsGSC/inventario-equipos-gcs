using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class LicenciasController : BaseController
{
    private readonly AppDbContext _db;
    public LicenciasController(AppDbContext db, PermisoService permisos) : base(permisos) => _db = db;

    public async Task<IActionResult> Index(bool? activo, string? buscar)
    {
        if (!await Puede("licencias.ver")) return AccesoDenegado();

        var query = _db.TiposLicencia.AsQueryable();
        if (activo.HasValue)
            query = query.Where(t => t.Activo == activo.Value);
        if (!string.IsNullOrEmpty(buscar))
            query = query.Where(t => t.Nombre.Contains(buscar));

        var tipos = await query.OrderBy(t => t.Nombre).ToListAsync();
        var tipoIds = tipos.Select(t => t.Id).ToList();

        var asignadasPorTipo = await _db.LicenciasAsignaciones
            .Where(la => tipoIds.Contains(la.TipoLicenciaId) && la.FechaDesvinculacion == null)
            .GroupBy(la => la.TipoLicenciaId)
            .Select(g => new { TipoLicenciaId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.TipoLicenciaId, g => g.Cantidad);

        ViewBag.AsignadasPorTipo = asignadasPorTipo;
        ViewBag.Activo = activo;
        ViewBag.Buscar = buscar;
        return View(tipos);
    }

    public async Task<IActionResult> Details(int id, int pagina = 1)
    {
        if (!await Puede("licencias.detalle")) return AccesoDenegado();

        const int tamPagina = 8;
        var tipo = await _db.TiposLicencia.FirstOrDefaultAsync(t => t.Id == id);
        if (tipo == null) return NotFound();

        ViewBag.AsignacionesActuales = await _db.LicenciasAsignaciones
            .Include(la => la.Empleado).ThenInclude(e => e!.Departamento)
            .Include(la => la.MiembroExterno)
            .Include(la => la.Grupo)
            .Include(la => la.Equipo)
            .Where(la => la.TipoLicenciaId == id && la.FechaDesvinculacion == null)
            .OrderByDescending(la => la.FechaAsignacion)
            .ToListAsync();

        var totalHistorial = await _db.LicenciasAsignaciones.CountAsync(la => la.TipoLicenciaId == id);
        ViewBag.Historial = await _db.LicenciasAsignaciones
            .Include(la => la.Empleado).ThenInclude(e => e!.Departamento)
            .Include(la => la.MiembroExterno)
            .Include(la => la.Grupo)
            .Include(la => la.Equipo)
            .Where(la => la.TipoLicenciaId == id)
            .OrderByDescending(la => la.FechaAsignacion)
            .Skip((pagina - 1) * tamPagina)
            .Take(tamPagina)
            .ToListAsync();
        ViewBag.Paginacion = new PaginacionViewModel
        {
            PaginaActual   = pagina,
            TotalPaginas   = (int)Math.Ceiling(totalHistorial / (double)tamPagina),
            TotalRegistros = totalHistorial,
            TamañoPagina   = tamPagina
        };
        return View(tipo);
    }

    // API: tipos de licencia activos, para el modal de "Licencias a entregar" en Movimientos/Registrar
    [HttpGet]
    public async Task<IActionResult> Disponibles()
    {
        var lista = await _db.TiposLicencia
            .Where(t => t.Activo)
            .OrderBy(t => t.Nombre)
            .Select(t => new { t.Id, t.Nombre })
            .ToListAsync();
        return Json(lista);
    }

    public async Task<IActionResult> Create()
    {
        if (!await Puede("licencias.crear")) return AccesoDenegado();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TipoLicencia tipo)
    {
        if (!await Puede("licencias.crear")) return AccesoDenegado();

        if (await _db.TiposLicencia.AnyAsync(t => t.Nombre == tipo.Nombre))
            ModelState.AddModelError("Nombre", "Ya existe un tipo de licencia con ese nombre.");

        if (!ModelState.IsValid)
            return View(tipo);

        _db.TiposLicencia.Add(tipo);
        await _db.SaveChangesAsync();
        TempData["OK"] = "Tipo de licencia registrado correctamente.";
        return RedirectToAction(nameof(Details), new { id = tipo.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        if (!await Puede("licencias.editar")) return AccesoDenegado();

        var tipo = await _db.TiposLicencia.FindAsync(id);
        if (tipo == null) return NotFound();
        return View(tipo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TipoLicencia tipo)
    {
        if (!await Puede("licencias.editar")) return AccesoDenegado();

        if (id != tipo.Id) return BadRequest();
        if (await _db.TiposLicencia.AnyAsync(t => t.Nombre == tipo.Nombre && t.Id != id))
            ModelState.AddModelError("Nombre", "Ya existe un tipo de licencia con ese nombre.");

        if (!ModelState.IsValid)
            return View(tipo);

        var original = await _db.TiposLicencia.FindAsync(id);
        if (original == null) return NotFound();
        original.Nombre = tipo.Nombre;
        original.CantidadTotal = tipo.CantidadTotal;
        original.Activo = tipo.Activo;
        await _db.SaveChangesAsync();
        TempData["OK"] = "Tipo de licencia actualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> AsignarDirecto(int id)
    {
        if (!await Puede("licencias.asignar")) return AccesoDenegado();

        var tipo = await _db.TiposLicencia.FindAsync(id);
        if (tipo == null) return NotFound();
        if (!tipo.Activo)
        {
            TempData["Error"] = "Este tipo de licencia está inactivo.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewBag.Empleados = await _db.Empleados.Where(e => e.Activo)
            .Include(e => e.Departamento).OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.MiembrosExternos = await _db.MiembrosExternos.Where(m => m.Activo).OrderBy(m => m.Nombre).ToListAsync();
        ViewBag.Grupos = await _db.Grupos.Where(g => g.Activo).OrderBy(g => g.Nombre).ToListAsync();
        return View(tipo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarDirecto(int id, string tipoResponsable,
        int? empleadoId, int? miembroExternoId, int? grupoId, string? observaciones)
    {
        if (!await Puede("licencias.asignar")) return AccesoDenegado();

        var tipo = await _db.TiposLicencia.FindAsync(id);
        if (tipo == null) return NotFound();

        int? nuevoEmpleadoId = tipoResponsable == "MiembroExterno" || tipoResponsable == "Grupo" ? null : empleadoId;
        int? nuevoMiembroExternoId = tipoResponsable == "MiembroExterno" ? miembroExternoId : null;
        int? nuevoGrupoId = tipoResponsable == "Grupo" ? grupoId : null;
        if (nuevoEmpleadoId == null && nuevoMiembroExternoId == null && nuevoGrupoId == null)
        {
            TempData["Error"] = "Debe seleccionar un responsable.";
            return RedirectToAction(nameof(AsignarDirecto), new { id });
        }

        _db.LicenciasAsignaciones.Add(new LicenciaAsignacion
        {
            TipoLicenciaId   = id,
            EmpleadoId       = nuevoEmpleadoId,
            MiembroExternoId = nuevoMiembroExternoId,
            GrupoId          = nuevoGrupoId,
            TipoAsignacion   = "Directo",
            TipoMovimiento   = "Asignacion",
            FechaAsignacion  = DateTime.Now,
            Observaciones    = observaciones
        });
        await _db.SaveChangesAsync();

        TempData["OK"] = "Licencia asignada correctamente.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Devolver(int asignacionId)
    {
        if (!await Puede("licencias.asignar")) return AccesoDenegado();

        var asignacion = await _db.LicenciasAsignaciones
            .Include(la => la.TipoLicencia)
            .Include(la => la.Empleado)
            .Include(la => la.MiembroExterno)
            .Include(la => la.Grupo)
            .FirstOrDefaultAsync(la => la.Id == asignacionId && la.FechaDesvinculacion == null);
        if (asignacion == null) return NotFound();

        return View(asignacion);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Devolver(int asignacionId, string? observaciones)
    {
        if (!await Puede("licencias.asignar")) return AccesoDenegado();

        var asignacion = await _db.LicenciasAsignaciones.FindAsync(asignacionId);
        if (asignacion == null || asignacion.FechaDesvinculacion != null) return NotFound();

        var ahora = DateTime.Now;
        asignacion.FechaDesvinculacion = ahora;

        _db.LicenciasAsignaciones.Add(new LicenciaAsignacion
        {
            TipoLicenciaId      = asignacion.TipoLicenciaId,
            EquipoId            = asignacion.EquipoId,
            EmpleadoId          = asignacion.EmpleadoId,
            MiembroExternoId    = asignacion.MiembroExternoId,
            GrupoId             = asignacion.GrupoId,
            TipoAsignacion      = asignacion.TipoAsignacion,
            TipoMovimiento      = "Devolucion",
            FechaAsignacion     = ahora,
            FechaDesvinculacion = ahora,
            Observaciones       = observaciones
        });
        await _db.SaveChangesAsync();

        TempData["OK"] = "Licencia revocada correctamente.";
        return RedirectToAction(nameof(Details), new { id = asignacion.TipoLicenciaId });
    }
}
