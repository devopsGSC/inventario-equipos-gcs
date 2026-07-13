using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class EquiposController : BaseController
{
    private readonly AppDbContext _db;
    public EquiposController(AppDbContext db, PermisoService permisos) : base(permisos) => _db = db;

    public async Task<IActionResult> Index(string? estado, string? tipo, string? buscar, bool? garantiaVencida, bool? garantiaPorVencer, int pagina = 1, int? paraEmpleado = null, int? paraMiembroExterno = null, int? paraGrupo = null)
    {
        if (!await Puede("equipos.ver")) return AccesoDenegado();

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
        if (garantiaVencida == true)
            query = query.Where(e => e.FechaGarantia.HasValue &&
                                     e.FechaGarantia.Value < DateTime.Today &&
                                     e.Estado != "Baja");
        if (garantiaPorVencer == true)
            query = query.Where(e => e.FechaGarantia.HasValue &&
                                     e.FechaGarantia.Value >= DateTime.Today &&
                                     e.FechaGarantia.Value <= DateTime.Today.AddDays(30) &&
                                     e.Estado != "Baja");

        var total = await query.CountAsync();
        var equipos = await query
            .OrderByDescending(e => e.FechaRegistro)
            .Skip((pagina - 1) * tamPagina)
            .Take(tamPagina)
            .ToListAsync();

        ViewBag.Estado = estado;
        ViewBag.Tipo   = tipo;
        ViewBag.Buscar = buscar;
        ViewBag.GarantiaVencida   = garantiaVencida;
        ViewBag.GarantiaPorVencer = garantiaPorVencer;

        ViewBag.ParaEmpleado = null;
        ViewBag.ParaMiembroExterno = null;
        ViewBag.ParaGrupo = null;
        if (paraEmpleado.HasValue)
        {
            var empleadoDestino = await _db.Empleados.FindAsync(paraEmpleado.Value);
            if (empleadoDestino != null)
            {
                ViewBag.ParaEmpleado = empleadoDestino.Id;
                ViewBag.NombreDestino = empleadoDestino.Nombre;
                ViewBag.ControladorDestino = "Empleados";
            }
        }
        else if (paraMiembroExterno.HasValue)
        {
            var miembroDestino = await _db.MiembrosExternos.FindAsync(paraMiembroExterno.Value);
            if (miembroDestino != null)
            {
                ViewBag.ParaMiembroExterno = miembroDestino.Id;
                ViewBag.NombreDestino = miembroDestino.Nombre;
                ViewBag.ControladorDestino = "MiembrosExternos";
            }
        }
        else if (paraGrupo.HasValue)
        {
            var grupoDestino = await _db.Grupos.FindAsync(paraGrupo.Value);
            if (grupoDestino != null)
            {
                ViewBag.ParaGrupo = grupoDestino.Id;
                ViewBag.NombreDestino = grupoDestino.Nombre;
                ViewBag.ControladorDestino = "Grupos";
            }
        }

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
        if (!await Puede("equipos.detalle")) return AccesoDenegado();

        const int tamPagina = 8;
        var equipo = await _db.Equipos
            .Include(e => e.TipoEquipo)
            .Include(e => e.PlanData)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (equipo == null) return NotFound();

        var totalMovimientos = await _db.Movimientos.CountAsync(m => m.EquipoId == id);

        var historial = await _db.Movimientos
            .Include(m => m.Empleado).ThenInclude(emp => emp!.Departamento)
            .Include(m => m.MiembroExterno)
            .Include(m => m.Grupo)
            .Include(m => m.Sitio)
            .Include(m => m.Imagenes.OrderBy(i => i.Orden))
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

        ViewBag.LicenciasActuales = await _db.LicenciasAsignaciones
            .Include(la => la.TipoLicencia)
            .Where(la => la.EquipoId == id && la.FechaDesvinculacion == null)
            .ToListAsync();

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
        if (!await Puede("equipos.crear")) return AccesoDenegado();

        ViewBag.Tipos = await _db.TiposEquipo
            .Where(t => t.Nombre != "Desktop" && t.Nombre != "Monitor" && t.Nombre != "Impresora")
            .OrderBy(t => t.Nombre)
            .ToListAsync();
        ViewBag.Planes = await _db.PlanesData.OrderBy(p => p.Nombre).ToListAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> NuevoTipo([FromBody] string nombre)
    {
        if (!await Puede("equipos.tipos.crear")) return Forbid();
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

    [HttpDelete]
    public async Task<IActionResult> EliminarTipo(int id)
    {
        if (!await Puede("equipos.tipos.eliminar")) return Forbid();
        var tipo = await _db.TiposEquipo.FindAsync(id);
        if (tipo == null) return NotFound();
        var enUso = await _db.Equipos.CountAsync(e => e.TipoEquipoId == id);
        if (enUso > 0)
            return BadRequest($"Tipo en uso por {enUso} equipo(s). No se puede eliminar.");
        _db.TiposEquipo.Remove(tipo);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> ListarTipos()
    {
        var lista = await _db.TiposEquipo
            .Where(t => t.Nombre != "Desktop" && t.Nombre != "Monitor" && t.Nombre != "Impresora")
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Nombre })
            .ToListAsync();
        return Json(lista);
    }

    [HttpGet]
    public async Task<IActionResult> ListarAccesorios()
    {
        var lista = await _db.AccesoriosEquipo
            .OrderBy(a => a.Nombre)
            .Select(a => new { a.Id, Nombre = a.Nombre })
            .ToListAsync();
        return Json(lista);
    }

    [HttpPost]
    public async Task<IActionResult> NuevoAccesorio([FromBody] string nombre)
    {
        if (!await PuedeAlguno("equipos.crear", "equipos.editar")) return Forbid();
        if (string.IsNullOrWhiteSpace(nombre)) return BadRequest("Nombre vacío.");
        nombre = nombre.Trim();
        var existe = await _db.AccesoriosEquipo.FirstOrDefaultAsync(a => a.Nombre == nombre);
        if (existe != null) return Ok(new { id = existe.Id, nombre = existe.Nombre });
        var acc = new AccesorioEquipo { Nombre = nombre };
        _db.AccesoriosEquipo.Add(acc);
        await _db.SaveChangesAsync();
        return Ok(new { id = acc.Id, nombre = acc.Nombre });
    }

    [HttpDelete]
    public async Task<IActionResult> EliminarAccesorio(int id)
    {
        if (!await PuedeAlguno("equipos.crear", "equipos.editar")) return Forbid();
        var acc = await _db.AccesoriosEquipo.FindAsync(id);
        if (acc == null) return NotFound();
        if (acc.Nombre == "Cargador" || acc.Nombre == "Funda")
            return BadRequest("No se pueden eliminar los accesorios fijos.");
        _db.AccesoriosEquipo.Remove(acc);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private static readonly string[] PlanesFijos = { "ESP 48", "ESP 23", "ESP 15" };

    [HttpGet]
    public async Task<IActionResult> ListarPlanes() =>
        Json(await _db.PlanesData.OrderBy(p => p.Nombre)
            .Select(p => new { p.Id, p.Nombre }).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> NuevoPlan([FromBody] string nombre)
    {
        if (!await Puede("equipos.planes.crear")) return Forbid();
        if (string.IsNullOrWhiteSpace(nombre)) return BadRequest("Nombre requerido.");
        var existente = await _db.PlanesData.FirstOrDefaultAsync(p => p.Nombre == nombre.Trim());
        if (existente != null) return Ok(new { id = existente.Id, nombre = existente.Nombre });
        var plan = new PlanData { Nombre = nombre.Trim() };
        _db.PlanesData.Add(plan);
        await _db.SaveChangesAsync();
        return Ok(new { id = plan.Id, nombre = plan.Nombre });
    }

    [HttpDelete]
    public async Task<IActionResult> EliminarPlan(int id)
    {
        if (!await Puede("equipos.planes.eliminar")) return Forbid();
        var plan = await _db.PlanesData.FindAsync(id);
        if (plan == null) return NotFound();
        if (PlanesFijos.Contains(plan.Nombre))
            return BadRequest("Este plan no se puede eliminar.");
        var enUso = await _db.Equipos.CountAsync(e => e.PlanDataId == id);
        if (enUso > 0)
            return BadRequest($"No se puede eliminar: {enUso} equipo(s) lo usan.");
        _db.PlanesData.Remove(plan);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Equipo equipo)
    {
        if (!await Puede("equipos.crear")) return AccesoDenegado();

        if (await _db.Equipos.AnyAsync(e => e.NumeroSerie == equipo.NumeroSerie))
            ModelState.AddModelError("NumeroSerie", "Ya existe un equipo con ese número de serie.");
        if (await _db.Equipos.AnyAsync(e => e.NombreEquipo == equipo.NombreEquipo))
            ModelState.AddModelError("NombreEquipo", "Ya existe un equipo con ese nombre.");
        if ((await _db.TiposEquipo.FindAsync(equipo.TipoEquipoId))?.Nombre == "Teléfono")
        {
            if (string.IsNullOrWhiteSpace(equipo.IMEI))
                ModelState.AddModelError("IMEI", "El IMEI es requerido para teléfonos.");
            if (string.IsNullOrWhiteSpace(equipo.NumeroSerie))
                ModelState.AddModelError("NumeroSerie", "El número de serie es requerido para teléfonos.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Tipos = await _db.TiposEquipo
                .Where(t => t.Nombre != "Desktop" && t.Nombre != "Monitor" && t.Nombre != "Impresora")
                .OrderBy(t => t.Nombre)
                .ToListAsync();
            ViewBag.Planes = await _db.PlanesData.OrderBy(p => p.Nombre).ToListAsync();
            ViewBag.SelectedTipoId = equipo.TipoEquipoId;
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
        if (!await Puede("equipos.editar")) return AccesoDenegado();

        var equipo = await _db.Equipos
            .Include(e => e.TipoEquipo)
            .Include(e => e.PlanData)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (equipo == null) return NotFound();
        ViewBag.Tipos = await _db.TiposEquipo
            .Where(t => t.Nombre != "Desktop" && t.Nombre != "Monitor" && t.Nombre != "Impresora")
            .OrderBy(t => t.Nombre)
            .ToListAsync();
        ViewBag.Planes = await _db.PlanesData.OrderBy(p => p.Nombre).ToListAsync();
        return View(equipo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Equipo equipo)
    {
        if (!await Puede("equipos.editar")) return AccesoDenegado();

        if (id != equipo.Id) return BadRequest();
        if (await _db.Equipos.AnyAsync(e => e.NumeroSerie == equipo.NumeroSerie && e.Id != id))
            ModelState.AddModelError("NumeroSerie", "Ya existe un equipo con ese número de serie.");
        if (await _db.Equipos.AnyAsync(e => e.NombreEquipo == equipo.NombreEquipo && e.Id != id))
            ModelState.AddModelError("NombreEquipo", "Ya existe un equipo con ese nombre.");
        if ((await _db.TiposEquipo.FindAsync(equipo.TipoEquipoId))?.Nombre == "Teléfono")
        {
            if (string.IsNullOrWhiteSpace(equipo.IMEI))
                ModelState.AddModelError("IMEI", "El IMEI es requerido para teléfonos.");
            if (string.IsNullOrWhiteSpace(equipo.NumeroSerie))
                ModelState.AddModelError("NumeroSerie", "El número de serie es requerido para teléfonos.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Tipos = await _db.TiposEquipo
                .Where(t => t.Nombre != "Desktop" && t.Nombre != "Monitor" && t.Nombre != "Impresora")
                .OrderBy(t => t.Nombre)
                .ToListAsync();
            ViewBag.Planes = await _db.PlanesData.OrderBy(p => p.Nombre).ToListAsync();
            return View(equipo);
        }
        var original = await _db.Equipos.FindAsync(id);
        if (original == null) return NotFound();
        original.NombreEquipo = equipo.NombreEquipo;
        original.Marca = equipo.Marca;
        original.Modelo = equipo.Modelo;
        original.NumeroSerie = equipo.NumeroSerie;
        original.TipoEquipoId = equipo.TipoEquipoId;
        original.IMEI = equipo.IMEI;
        original.Accesorios = equipo.Accesorios;
        original.Costo = equipo.Costo;
        original.FechaCompra = equipo.FechaCompra;
        original.FechaGarantia = equipo.FechaGarantia;
        original.RAM = equipo.RAM;
        original.Procesador = equipo.Procesador;
        original.Almacenamiento = equipo.Almacenamiento;
        original.PlanDataId = equipo.PlanDataId;
        await _db.SaveChangesAsync();
        TempData["OK"] = "Equipo actualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, IgnoreAntiforgeryToken]
    public async Task<IActionResult> AdjuntarPeriferico([FromBody] AdjuntarPerifericoDto dto)
    {
        if (!await Puede("equipos.editar")) return Forbid();

        var equipo = await _db.Equipos.FindAsync(dto.EquipoId);
        if (equipo == null) return NotFound();
        if (equipo.Estado != "Asignado" && equipo.Estado != "Prestamo")
            return BadRequest("El equipo debe estar Asignado o en Préstamo.");

        var periferico = await _db.Perifericos.FindAsync(dto.PerifericoId);
        if (periferico == null || periferico.Estado != "Disponible")
            return BadRequest("El periférico no está disponible.");

        var movActivo = await _db.Movimientos.FirstOrDefaultAsync(m =>
            m.EquipoId == dto.EquipoId && m.FechaDevolucion == null &&
            (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"));

        periferico.Estado = "Asignado";
        _db.EquiposPerifericos.Add(new EquipoPeriferico
        {
            EquipoId                = dto.EquipoId,
            PerifericoId            = dto.PerifericoId,
            EmpleadoId              = movActivo?.EmpleadoId,
            MiembroExternoId        = movActivo?.MiembroExternoId,
            GrupoId                 = movActivo?.GrupoId,
            TipoMovimiento          = movActivo?.TipoMovimiento ?? "Asignacion",
            FechaAsignacion         = DateTime.Now,
            FechaDevolucionEstimada = movActivo?.FechaFinEstimada,
            SitioId                 = movActivo?.SitioId
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    public record AdjuntarPerifericoDto(int EquipoId, int PerifericoId);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Dar_De_Baja(int id)
    {
        if (!await Puede("equipos.baja")) return AccesoDenegado();

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
        if (!await Puede("equipos.baja")) return AccesoDenegado();

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
