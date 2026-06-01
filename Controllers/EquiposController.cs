using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class EquiposController : Controller
{
    private readonly AppDbContext _db;
    public EquiposController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? estado, string? tipo, string? buscar, int pagina = 1)
    {
        const int tamPagina = 12;
        var query = _db.Equipos.Include(e => e.TipoEquipo).AsQueryable();

        if (!string.IsNullOrEmpty(estado))
            query = query.Where(e => e.Estado == estado);
        if (!string.IsNullOrEmpty(tipo))
            query = query.Where(e => e.TipoEquipo!.Nombre == tipo);
        if (!string.IsNullOrEmpty(buscar))
            query = query.Where(e => e.NumeroSerie.Contains(buscar) ||
                                     e.NombreEquipo.Contains(buscar) ||
                                     e.Marca.Contains(buscar));

        var total = await query.CountAsync();
        var equipos = await query
            .OrderByDescending(e => e.FechaRegistro)
            .Skip((pagina - 1) * tamPagina)
            .Take(tamPagina)
            .ToListAsync();

        ViewBag.Estado = estado;
        ViewBag.Tipo   = tipo;
        ViewBag.Buscar = buscar;
        ViewBag.Tipos  = await _db.TiposEquipo
            .Where(t => t.Nombre != "Desktop" && t.Nombre != "Monitor" && t.Nombre != "Impresora")
            .OrderBy(t => t.Nombre).Select(t => t.Nombre).ToListAsync();
        ViewBag.Paginacion = new PaginacionViewModel
        {
            PaginaActual   = pagina,
            TotalPaginas   = (int)Math.Ceiling(total / (double)tamPagina),
            TotalRegistros = total,
            TamañoPagina   = tamPagina
        };
        return View(equipos);
    }

    public async Task<IActionResult> Details(int id, int pagina = 1)
    {
        const int tamPagina = 8;
        var equipo = await _db.Equipos
            .Include(e => e.TipoEquipo)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (equipo == null) return NotFound();

        var totalMovimientos = await _db.Movimientos.CountAsync(m => m.EquipoId == id);

        var historial = await _db.Movimientos
            .Include(m => m.Empleado).ThenInclude(emp => emp!.Departamento)
            .Where(m => m.EquipoId == id)
            .OrderByDescending(m => m.FechaInicio)
            .Skip((pagina - 1) * tamPagina)
            .Take(tamPagina)
            .ToListAsync();

        var perifsActivos = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Where(ep => ep.EquipoId == id && ep.FechaDesvinculacion == null)
            .ToListAsync();
        ViewBag.PerifeicosActivos = perifsActivos;

        ViewBag.Paginacion = new PaginacionViewModel
        {
            PaginaActual   = pagina,
            TotalPaginas   = (int)Math.Ceiling(totalMovimientos / (double)tamPagina),
            TotalRegistros = totalMovimientos,
            TamañoPagina   = tamPagina
        };

        var vm = new EquipoDetalleViewModel
        {
            Equipo = equipo,
            Historial = historial,
            MovimientoActivo = historial.FirstOrDefault(m =>
                m.FechaDevolucion == null &&
                new[] { "Asignacion", "Prestamo", "EntradaGarantia" }.Contains(m.TipoMovimiento))
        };
        return View(vm);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Tipos = new SelectList(await _db.TiposEquipo.ToListAsync(), "Id", "Nombre");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> NuevoTipo([FromBody] string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest();
        var existe = await _db.TiposEquipo.FirstOrDefaultAsync(t => t.Nombre == nombre.Trim());
        if (existe != null)
            return Ok(new { id = existe.Id, nombre = existe.Nombre });
        var tipo = new TipoEquipo { Nombre = nombre.Trim() };
        _db.TiposEquipo.Add(tipo);
        await _db.SaveChangesAsync();
        return Ok(new { id = tipo.Id, nombre = tipo.Nombre });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Equipo equipo)
    {
        if (await _db.Equipos.AnyAsync(e => e.NumeroSerie == equipo.NumeroSerie))
            ModelState.AddModelError("NumeroSerie", "Ya existe un equipo con ese número de serie.");
        if (await _db.Equipos.AnyAsync(e => e.NombreEquipo == equipo.NombreEquipo))
            ModelState.AddModelError("NombreEquipo", "Ya existe un equipo con ese nombre.");

        if (!ModelState.IsValid)
        {
            ViewBag.Tipos = new SelectList(await _db.TiposEquipo.ToListAsync(), "Id", "Nombre");
            return View(equipo);
        }
        equipo.Estado = "Bodega";
        equipo.FechaRegistro = DateTime.Now;
        _db.Equipos.Add(equipo);
        await _db.SaveChangesAsync();
        TempData["OK"] = "Equipo registrado correctamente.";
        return RedirectToAction(nameof(Details), new { id = equipo.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var equipo = await _db.Equipos.FindAsync(id);
        if (equipo == null) return NotFound();
        ViewBag.Tipos = new SelectList(await _db.TiposEquipo.ToListAsync(), "Id", "Nombre", equipo.TipoEquipoId);
        return View(equipo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Equipo equipo)
    {
        if (id != equipo.Id) return BadRequest();
        if (await _db.Equipos.AnyAsync(e => e.NumeroSerie == equipo.NumeroSerie && e.Id != id))
            ModelState.AddModelError("NumeroSerie", "Ya existe un equipo con ese número de serie.");
        if (await _db.Equipos.AnyAsync(e => e.NombreEquipo == equipo.NombreEquipo && e.Id != id))
            ModelState.AddModelError("NombreEquipo", "Ya existe un equipo con ese nombre.");

        if (!ModelState.IsValid)
        {
            ViewBag.Tipos = new SelectList(await _db.TiposEquipo.ToListAsync(), "Id", "Nombre", equipo.TipoEquipoId);
            return View(equipo);
        }
        var original = await _db.Equipos.FindAsync(id);
        if (original == null) return NotFound();
        original.NombreEquipo = equipo.NombreEquipo;
        original.Marca = equipo.Marca;
        original.Modelo = equipo.Modelo;
        original.NumeroSerie = equipo.NumeroSerie;
        original.TipoEquipoId = equipo.TipoEquipoId;
        original.Accesorios = equipo.Accesorios;
        original.Costo = equipo.Costo;
        original.FechaCompra = equipo.FechaCompra;
        original.FechaGarantia = equipo.FechaGarantia;
        await _db.SaveChangesAsync();
        TempData["OK"] = "Equipo actualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, IgnoreAntiforgeryToken]
    public async Task<IActionResult> AdjuntarPeriferico([FromBody] AdjuntarPerifericoDto dto)
    {
        var equipo = await _db.Equipos.FindAsync(dto.EquipoId);
        if (equipo == null) return NotFound();
        if (equipo.Estado != "Asignado" && equipo.Estado != "Prestamo")
            return BadRequest("El equipo debe estar Asignado o en Préstamo.");

        var periferico = await _db.Perifericos.FindAsync(dto.PerifericoId);
        if (periferico == null || periferico.Estado != "Disponible")
            return BadRequest("El periférico no está disponible.");

        periferico.Estado = "Asignado";
        _db.EquiposPerifericos.Add(new EquipoPeriferico
        {
            EquipoId        = dto.EquipoId,
            PerifericoId    = dto.PerifericoId,
            FechaAsignacion = DateTime.Now
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    public record AdjuntarPerifericoDto(int EquipoId, int PerifericoId);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Dar_De_Baja(int id)
    {
        var equipo = await _db.Equipos.FindAsync(id);
        if (equipo == null) return NotFound();
        if (equipo.Estado != "Bodega")
        {
            TempData["Error"] = "Solo se pueden dar de baja equipos en Bodega.";
            return RedirectToAction(nameof(Details), new { id });
        }
        equipo.Estado = "Baja";
        _db.Movimientos.Add(new Movimiento
        {
            EquipoId = id, TipoMovimiento = "Baja", FechaInicio = DateTime.Now,
            Observaciones = "Equipo dado de baja del inventario."
        });
        await _db.SaveChangesAsync();
        TempData["OK"] = "Equipo dado de baja.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivar(int id)
    {
        var equipo = await _db.Equipos.FindAsync(id);
        if (equipo == null) return NotFound();
        if (equipo.Estado != "Baja")
        {
            TempData["Error"] = "Solo se pueden reactivar equipos dados de baja.";
            return RedirectToAction(nameof(Details), new { id });
        }
        equipo.Estado = "Bodega";
        _db.Movimientos.Add(new Movimiento
        {
            EquipoId = id, TipoMovimiento = "Reactivacion", FechaInicio = DateTime.Now,
            Observaciones = "Equipo reactivado y disponible en bodega."
        });
        await _db.SaveChangesAsync();
        TempData["OK"] = "Equipo reactivado. Ahora está disponible en bodega.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
