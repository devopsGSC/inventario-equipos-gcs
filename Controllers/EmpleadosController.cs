using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;

namespace InventarioTI.Controllers;

public class EmpleadosController : Controller
{
    private readonly AppDbContext _db;
    public EmpleadosController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? buscar)
    {
        var query = _db.Empleados.Include(e => e.Departamento).AsQueryable();
        if (!string.IsNullOrEmpty(buscar))
            query = query.Where(e => e.Nombre.Contains(buscar) ||
                                     e.CodigoEmpleado.Contains(buscar) ||
                                     e.DUI.Contains(buscar));
        ViewBag.Buscar = buscar;
        return View(await query.OrderBy(e => e.Nombre).ToListAsync());
    }

    public async Task<IActionResult> Details(int id, int pagina = 1)
    {
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

    public async Task<IActionResult> Create()
    {
        ViewBag.Departamentos = new SelectList(await _db.Departamentos.ToListAsync(), "Id", "Nombre");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Empleado empleado)
    {
        if (await _db.Empleados.AnyAsync(e => e.CodigoEmpleado == empleado.CodigoEmpleado))
            ModelState.AddModelError("CodigoEmpleado", "Ya existe un empleado con ese código.");
        if (await _db.Empleados.AnyAsync(e => e.DUI == empleado.DUI))
            ModelState.AddModelError("DUI", "Ya existe un empleado con ese DUI.");

        if (!ModelState.IsValid)
        {
            ViewBag.Departamentos = new SelectList(await _db.Departamentos.ToListAsync(), "Id", "Nombre");
            return View(empleado);
        }
        _db.Empleados.Add(empleado);
        await _db.SaveChangesAsync();
        TempData["OK"] = "Empleado registrado correctamente.";
        return RedirectToAction(nameof(Details), new { id = empleado.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var empleado = await _db.Empleados.FindAsync(id);
        if (empleado == null) return NotFound();
        ViewBag.Departamentos = new SelectList(await _db.Departamentos.ToListAsync(), "Id", "Nombre", empleado.DepartamentoId);
        return View(empleado);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Empleado empleado)
    {
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
