using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;

namespace InventarioTI.Controllers;

public class PerifericosController : Controller
{
    private readonly AppDbContext _db;
    public PerifericosController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? estado, string? buscar)
    {
        var query = _db.Perifericos.Include(p => p.TipoPeriferico).AsQueryable();
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(p => p.Estado == estado);
        if (!string.IsNullOrEmpty(buscar))
            query = query.Where(p =>
                p.NumeroSerie.Contains(buscar) ||
                p.Marca.Contains(buscar) ||
                p.Modelo.Contains(buscar));
        ViewBag.Estado = estado;
        ViewBag.Buscar = buscar;
        return View(await query.OrderByDescending(p => p.FechaRegistro).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var p = await _db.Perifericos
            .Include(p => p.TipoPeriferico)
            .Include(p => p.EquiposPerifericos)
                .ThenInclude(ep => ep.Equipo).ThenInclude(e => e!.TipoEquipo)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (p == null) return NotFound();

        var asignacionActiva = p.EquiposPerifericos
            .FirstOrDefault(ep => ep.FechaDesvinculacion == null);
        if (asignacionActiva != null)
        {
            var movActivo = await _db.Movimientos
                .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
                .Where(m => m.EquipoId == asignacionActiva.EquipoId &&
                            m.FechaDevolucion == null &&
                            (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
                .OrderByDescending(m => m.FechaInicio)
                .FirstOrDefaultAsync();
            ViewBag.MovimientoActivo = movActivo;
            ViewBag.AsignacionActiva = asignacionActiva;
        }
        return View(p);
    }


    public async Task<IActionResult> Create()
    {
        ViewBag.Tipos = new SelectList(await _db.TiposPerifericos.ToListAsync(), "Id", "Nombre");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Periferico p)
    {
        if (await _db.Perifericos.AnyAsync(x => x.NumeroSerie == p.NumeroSerie))
            ModelState.AddModelError("NumeroSerie", "Ya existe un periférico con ese número de serie.");
        if (!ModelState.IsValid)
        {
            ViewBag.Tipos = new SelectList(await _db.TiposPerifericos.ToListAsync(), "Id", "Nombre");
            return View(p);
        }
        p.Estado = "Disponible";
        p.FechaRegistro = DateTime.Now;
        _db.Perifericos.Add(p);
        await _db.SaveChangesAsync();
        TempData["OK"] = "Periférico registrado correctamente.";
        return RedirectToAction(nameof(Details), new { id = p.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var p = await _db.Perifericos.FindAsync(id);
        if (p == null) return NotFound();
        ViewBag.Tipos = new SelectList(await _db.TiposPerifericos.ToListAsync(), "Id", "Nombre", p.TipoPerifericoId);
        return View(p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Periferico p)
    {
        if (id != p.Id) return BadRequest();
        if (await _db.Perifericos.AnyAsync(x => x.NumeroSerie == p.NumeroSerie && x.Id != id))
            ModelState.AddModelError("NumeroSerie", "Ya existe un periférico con ese número de serie.");
        if (!ModelState.IsValid)
        {
            ViewBag.Tipos = new SelectList(await _db.TiposPerifericos.ToListAsync(), "Id", "Nombre", p.TipoPerifericoId);
            return View(p);
        }
        var orig = await _db.Perifericos.FindAsync(id);
        if (orig == null) return NotFound();
        orig.TipoPerifericoId = p.TipoPerifericoId;
        orig.Marca    = p.Marca;
        orig.Modelo   = p.Modelo;
        orig.NumeroSerie = p.NumeroSerie;
        orig.Observaciones = p.Observaciones;
        orig.FechaCompra = p.FechaCompra;
        await _db.SaveChangesAsync();
        TempData["OK"] = "Periférico actualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DarDeBaja(int id)
    {
        var p = await _db.Perifericos.FindAsync(id);
        if (p == null) return NotFound();
        if (p.Estado != "Disponible")
        {
            TempData["Error"] = "Solo se pueden dar de baja periféricos disponibles.";
            return RedirectToAction(nameof(Details), new { id });
        }
        p.Estado = "Baja";
        await _db.SaveChangesAsync();
        TempData["OK"] = "Periférico dado de baja.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivar(int id)
    {
        var p = await _db.Perifericos.FindAsync(id);
        if (p == null) return NotFound();
        if (p.Estado != "Baja")
        {
            TempData["Error"] = "Solo se pueden reactivar periféricos dados de baja.";
            return RedirectToAction(nameof(Details), new { id });
        }
        p.Estado = "Disponible";
        await _db.SaveChangesAsync();
        TempData["OK"] = "Periférico reactivado. Ahora está disponible en stock.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // API: nuevo tipo de periférico on-the-fly
    [HttpPost]
    public async Task<IActionResult> NuevoTipo([FromBody] string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return BadRequest();
        var existe = await _db.TiposPerifericos.FirstOrDefaultAsync(t => t.Nombre == nombre.Trim());
        if (existe != null) return Ok(new { id = existe.Id, nombre = existe.Nombre });
        var tipo = new TipoPeriferico { Nombre = nombre.Trim() };
        _db.TiposPerifericos.Add(tipo);
        await _db.SaveChangesAsync();
        return Ok(new { id = tipo.Id, nombre = tipo.Nombre });
    }

    // API: periféricos disponibles para adjuntar (JSON)
    [HttpGet]
    public async Task<IActionResult> Disponibles()
    {
        var lista = await _db.Perifericos
            .Include(p => p.TipoPeriferico)
            .Where(p => p.Estado == "Disponible")
            .Select(p => new { p.Id, p.Marca, p.Modelo, p.NumeroSerie, tipo = p.TipoPeriferico!.Nombre })
            .ToListAsync();
        return Json(lista);
    }
}
