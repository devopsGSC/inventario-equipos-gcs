using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class GruposController : BaseController
{
    private readonly AppDbContext _db;
    public GruposController(AppDbContext db, PermisoService permisos) : base(permisos) => _db = db;

    public async Task<IActionResult> Index(bool? activo, string? buscar, int pagina = 1)
    {
        if (!await Puede("grupos.ver")) return AccesoDenegado();

        const int tamPagina = 12;
        var query = _db.Grupos.AsQueryable();

        if (activo.HasValue)
            query = query.Where(g => g.Activo == activo.Value);
        if (!string.IsNullOrEmpty(buscar))
            query = query.Where(g => g.Nombre.Contains(buscar) ||
                                     (g.Descripcion != null && g.Descripcion.Contains(buscar)));

        var total = await query.CountAsync();
        var grupos = await query
            .OrderBy(g => g.Nombre)
            .Skip((pagina - 1) * tamPagina)
            .Take(tamPagina)
            .ToListAsync();

        ViewBag.Activo = activo;
        ViewBag.Buscar = buscar;
        ViewBag.Paginacion = new PaginacionViewModel
        {
            PaginaActual   = pagina,
            TotalPaginas   = (int)Math.Ceiling(total / (double)tamPagina),
            TotalRegistros = total,
            TamañoPagina   = tamPagina
        };
        return View(grupos);
    }

    public async Task<IActionResult> Details(int id, int pagina = 1)
    {
        if (!await Puede("grupos.detalle")) return AccesoDenegado();

        const int tamPagina = 8;
        var grupo = await _db.Grupos.FirstOrDefaultAsync(g => g.Id == id);
        if (grupo == null) return NotFound();

        var equiposActuales = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => m.GrupoId == id && m.FechaDevolucion == null &&
                        (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
            .ToListAsync();
        ViewBag.EquiposActuales = equiposActuales;

        ViewBag.PerifsActuales = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Equipo)
            .Where(ep => ep.GrupoId == id && ep.FechaDesvinculacion == null)
            .OrderByDescending(ep => ep.FechaAsignacion)
            .ToListAsync();

        ViewBag.HistorialPerifericos = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Equipo)
            .Where(ep => ep.GrupoId == id)
            .OrderByDescending(ep => ep.FechaAsignacion)
            .ToListAsync();

        var totalHistorial = await _db.Movimientos.CountAsync(m => m.GrupoId == id);
        ViewBag.Historial = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => m.GrupoId == id)
            .OrderByDescending(m => m.FechaInicio)
            .Skip((pagina - 1) * tamPagina).Take(tamPagina)
            .ToListAsync();
        ViewBag.Paginacion = new PaginacionViewModel
        {
            PaginaActual   = pagina,
            TotalPaginas   = (int)Math.Ceiling(totalHistorial / (double)tamPagina),
            TotalRegistros = totalHistorial,
            TamañoPagina   = tamPagina
        };
        return View(grupo);
    }

    public async Task<IActionResult> Create()
    {
        if (!await Puede("grupos.crear")) return AccesoDenegado();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Grupo grupo)
    {
        if (!await Puede("grupos.crear")) return AccesoDenegado();

        if (await _db.Grupos.AnyAsync(g => g.Nombre == grupo.Nombre))
            ModelState.AddModelError("Nombre", "Ya existe un grupo con ese nombre.");

        if (!ModelState.IsValid)
            return View(grupo);

        _db.Grupos.Add(grupo);
        await _db.SaveChangesAsync();
        TempData["OK"] = "Grupo registrado correctamente.";
        return RedirectToAction(nameof(Details), new { id = grupo.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        if (!await Puede("grupos.editar")) return AccesoDenegado();

        var grupo = await _db.Grupos.FindAsync(id);
        if (grupo == null) return NotFound();
        return View(grupo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Grupo grupo)
    {
        if (!await Puede("grupos.editar")) return AccesoDenegado();

        if (id != grupo.Id) return BadRequest();
        if (await _db.Grupos.AnyAsync(g => g.Nombre == grupo.Nombre && g.Id != id))
            ModelState.AddModelError("Nombre", "Ya existe un grupo con ese nombre.");

        if (!ModelState.IsValid)
            return View(grupo);

        var original = await _db.Grupos.FindAsync(id);
        if (original == null) return NotFound();
        original.Nombre = grupo.Nombre;
        original.Descripcion = grupo.Descripcion;
        original.Activo = grupo.Activo;
        await _db.SaveChangesAsync();
        TempData["OK"] = "Grupo actualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
