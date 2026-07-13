using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class PerifericosController : BaseController
{
    private readonly AppDbContext _db;
    private readonly PdfService _pdf;
    private readonly UserManager<UsuarioApp> _users;
    public PerifericosController(AppDbContext db, PdfService pdf, UserManager<UsuarioApp> users, PermisoService permisos) : base(permisos)
    { _db = db; _pdf = pdf; _users = users; }

    public async Task<IActionResult> Index(string? estado, string? buscar)
    {
        if (!await Puede("perifericos.ver")) return AccesoDenegado();

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

    public async Task<IActionResult> Details(int id, int pagina = 1)
    {
        if (!await Puede("perifericos.detalle")) return AccesoDenegado();

        const int tamPagina = 8;
        var p = await _db.Perifericos
            .Include(p => p.TipoPeriferico)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (p == null) return NotFound();

        // Historial completo paginado
        var totalHistorial = await _db.EquiposPerifericos
            .CountAsync(ep => ep.PerifericoId == id);

        var historial = await _db.EquiposPerifericos
            .Include(ep => ep.Equipo).ThenInclude(e => e!.TipoEquipo)
            .Include(ep => ep.Empleado).ThenInclude(e => e!.Departamento)
            .Include(ep => ep.MiembroExterno)
            .Include(ep => ep.Grupo)
            .Include(ep => ep.Sitio)
            .Include(ep => ep.Imagenes.OrderBy(i => i.Orden))
            .Where(ep => ep.PerifericoId == id)
            .OrderByDescending(ep => ep.FechaAsignacion)
            .Skip((pagina - 1) * tamPagina)
            .Take(tamPagina)
            .ToListAsync();

        ViewBag.Historial = historial;
        ViewBag.Paginacion = new PaginacionViewModel
        {
            PaginaActual   = pagina,
            TotalPaginas   = (int)Math.Ceiling(totalHistorial / (double)tamPagina),
            TotalRegistros = totalHistorial,
            TamañoPagina   = tamPagina
        };

        // Asignación activa actual
        var asignacionActiva = await _db.EquiposPerifericos
            .Include(ep => ep.Equipo)
            .Include(ep => ep.Empleado).ThenInclude(e => e!.Departamento)
            .Include(ep => ep.MiembroExterno)
            .Include(ep => ep.Grupo)
            .Include(ep => ep.Sitio)
            .Include(ep => ep.Imagenes.OrderBy(i => i.Orden))
            .FirstOrDefaultAsync(ep => ep.PerifericoId == id && ep.FechaDesvinculacion == null);

        if (asignacionActiva != null)
            ViewBag.AsignacionActiva = asignacionActiva;
        return View(p);
    }


    public async Task<IActionResult> Create()
    {
        if (!await Puede("perifericos.crear")) return AccesoDenegado();

        ViewBag.Tipos = new SelectList(await _db.TiposPerifericos.ToListAsync(), "Id", "Nombre");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Periferico p)
    {
        if (!await Puede("perifericos.crear")) return AccesoDenegado();

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
        if (!await Puede("perifericos.editar")) return AccesoDenegado();

        var p = await _db.Perifericos.FindAsync(id);
        if (p == null) return NotFound();
        ViewBag.Tipos = new SelectList(await _db.TiposPerifericos.ToListAsync(), "Id", "Nombre", p.TipoPerifericoId);
        return View(p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Periferico p)
    {
        if (!await Puede("perifericos.editar")) return AccesoDenegado();

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
        if (!await Puede("perifericos.baja")) return AccesoDenegado();

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
        if (!await Puede("perifericos.baja")) return AccesoDenegado();

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
        if (!await Puede("perifericos.tipos.crear")) return Forbid();
        if (string.IsNullOrWhiteSpace(nombre)) return BadRequest();
        var existe = await _db.TiposPerifericos.FirstOrDefaultAsync(t => t.Nombre == nombre.Trim());
        if (existe != null) return Ok(new { id = existe.Id, nombre = existe.Nombre });
        var tipo = new TipoPeriferico { Nombre = nombre.Trim() };
        _db.TiposPerifericos.Add(tipo);
        await _db.SaveChangesAsync();
        return Ok(new { id = tipo.Id, nombre = tipo.Nombre });
    }

    [HttpDelete]
    public async Task<IActionResult> EliminarTipo(int id)
    {
        if (!await Puede("perifericos.tipos.eliminar")) return Forbid();
        var tipo = await _db.TiposPerifericos.FindAsync(id);
        if (tipo == null) return NotFound();

        var tiposFijos = new[] { "Monitor", "Headset", "Mochila HP", "Teclado y Ratón" };
        if (tiposFijos.Contains(tipo.Nombre))
            return BadRequest("Este tipo no se puede eliminar.");

        var enUso = await _db.Perifericos.CountAsync(p => p.TipoPerifericoId == id);
        if (enUso > 0)
            return BadRequest($"No se puede eliminar: hay {enUso} periférico(s) con este tipo.");

        _db.TiposPerifericos.Remove(tipo);
        await _db.SaveChangesAsync();
        return Ok();
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

    public async Task<IActionResult> AsignarDirecto(int id)
    {
        if (!await Puede("perifericos.asignar")) return AccesoDenegado();

        var p = await _db.Perifericos.Include(p => p.TipoPeriferico)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (p == null) return NotFound();
        if (p.Estado != "Disponible")
        {
            TempData["Error"] = "Solo se pueden asignar directamente periféricos en estado Disponible.";
            return RedirectToAction(nameof(Details), new { id });
        }
        ViewBag.Empleados = await _db.Empleados.Where(e => e.Activo)
            .Include(e => e.Departamento)
            .OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.MiembrosExternos = await _db.MiembrosExternos.Where(m => m.Activo).OrderBy(m => m.Nombre).ToListAsync();
        ViewBag.Grupos = await _db.Grupos.Where(g => g.Activo).OrderBy(g => g.Nombre).ToListAsync();
        ViewBag.Sitios = await _db.Sitios.Where(s => s.Activo).OrderBy(s => s.Nombre).ToListAsync();
        return View(p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarDirecto(int id, string tipoResponsable,
        int? empleadoId, int? miembroExternoId, int? grupoId,
        string tipoMovimiento, DateTime? fechaDevolucionEstimada,
        string? observaciones, string? firmaEmpleado, int? sitioId)
    {
        if (!await Puede("perifericos.asignar")) return AccesoDenegado();

        bool esAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        var p = await _db.Perifericos.FindAsync(id);
        if (p == null) return NotFound();
        if (p.Estado != "Disponible")
        {
            TempData["Error"] = "El periférico ya no está disponible.";
            return RedirectToAction(nameof(Details), new { id });
        }

        int? nuevoEmpleadoId = tipoResponsable == "MiembroExterno" || tipoResponsable == "Grupo" ? null : empleadoId;
        int? nuevoMiembroExternoId = tipoResponsable == "MiembroExterno" ? miembroExternoId : null;
        int? nuevoGrupoId = tipoResponsable == "Grupo" ? grupoId : null;
        if (nuevoEmpleadoId == null && nuevoMiembroExternoId == null && nuevoGrupoId == null)
        {
            TempData["Error"] = "Debe seleccionar un responsable.";
            return RedirectToAction(nameof(AsignarDirecto), new { id });
        }

        var asignacion = new EquipoPeriferico
        {
            EquipoId                = null,
            PerifericoId            = id,
            EmpleadoId              = nuevoEmpleadoId,
            MiembroExternoId        = nuevoMiembroExternoId,
            GrupoId                 = nuevoGrupoId,
            TipoAsignacion          = "Directo",
            TipoMovimiento          = tipoMovimiento == "Prestamo" ? "Prestamo" : "Asignacion",
            FechaAsignacion         = DateTime.Now,
            FechaDevolucionEstimada = fechaDevolucionEstimada,
            Observaciones           = observaciones,
            FirmaEmpleado           = firmaEmpleado,
            SitioId                 = await Puede("movimientos.sitio") ? sitioId : null
        };
        p.Estado = "Asignado";
        _db.EquiposPerifericos.Add(asignacion);
        await _db.SaveChangesAsync();

        TempData["OK"] = $"{(asignacion.TipoMovimiento == "Prestamo" ? "Préstamo" : "Asignación")} registrada correctamente.";

        var redirectUrl = Url.Action(nameof(CartaDirecta), new { asignacionId = asignacion.Id })!;
        if (esAjax) return Json(new { asignacionId = asignacion.Id, redirectUrl });
        return Redirect(redirectUrl);
    }

    public async Task<IActionResult> CartaDirecta(int asignacionId)
    {
        if (!await Puede("perifericos.asignar")) return AccesoDenegado();

        var ep = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Empleado).ThenInclude(e => e!.Departamento)
            .Include(ep => ep.MiembroExterno)
            .Include(ep => ep.Grupo)
            .FirstOrDefaultAsync(ep => ep.Id == asignacionId);
        if (ep == null || ep.Periferico == null ||
            (ep.Empleado == null && ep.MiembroExterno == null && ep.Grupo == null))
            return NotFound();
        return View(ep);
    }

    public async Task<IActionResult> Devolver(int id)
    {
        if (!await Puede("perifericos.asignar")) return AccesoDenegado();

        var p = await _db.Perifericos
            .Include(p => p.TipoPeriferico)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (p == null) return NotFound();

        var asignacion = await _db.EquiposPerifericos
            .Include(ep => ep.Empleado)
            .Include(ep => ep.MiembroExterno)
            .Include(ep => ep.Grupo)
            .FirstOrDefaultAsync(ep => ep.PerifericoId == id &&
                                       ep.FechaDesvinculacion == null);
        if (asignacion == null)
        {
            TempData["Error"] = "Este periférico no tiene una asignación activa.";
            return RedirectToAction(nameof(Details), new { id });
        }
        ViewBag.Asignacion = asignacion;
        return View(p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Devolver(int id, string? observaciones, string? firmaEmpleado)
    {
        if (!await Puede("perifericos.asignar")) return AccesoDenegado();

        bool esAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        var asignacion = await _db.EquiposPerifericos
            .FirstOrDefaultAsync(ep => ep.PerifericoId == id &&
                                       ep.FechaDesvinculacion == null);
        if (asignacion == null) return NotFound();

        asignacion.FechaDesvinculacion = DateTime.Now;
        asignacion.TipoMovimiento      = "Devolucion";
        asignacion.Observaciones       = observaciones;
        if (!string.IsNullOrEmpty(firmaEmpleado))
            asignacion.FirmaEmpleado   = firmaEmpleado;

        var p = await _db.Perifericos.FindAsync(id);
        if (p != null) p.Estado = "Disponible";

        await _db.SaveChangesAsync();
        TempData["OK"] = "Devolución registrada. El periférico está disponible nuevamente.";

        var redirectUrl = Url.Action(nameof(Details), new { id })!;
        if (esAjax) return Json(new { asignacionId = asignacion.Id, redirectUrl });
        return Redirect(redirectUrl);
    }

    // Subir imágenes para una asignación directa de periférico (llamado via JS después de confirmar)
    [HttpPost]
    public async Task<IActionResult> SubirImagenesPeriferico(int epId,
        List<IFormFile> imagenes, List<string?> descripciones)
    {
        if (!await Puede("perifericos.asignar")) return Forbid();

        var asignacion = await _db.EquiposPerifericos.FindAsync(epId);
        if (asignacion == null) return NotFound();

        var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "movimientos");
        Directory.CreateDirectory(carpeta);

        int orden = 1;
        for (int i = 0; i < imagenes.Count; i++)
        {
            var archivo = imagenes[i];
            if (archivo.Length == 0) continue;
            if (archivo.Length > 10 * 1024 * 1024) continue; // max 10 MB por imagen

            var ext   = Path.GetExtension(archivo.FileName).ToLower();
            var valid = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!valid.Contains(ext)) continue;

            var nombre = $"perif_{epId}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}{ext}";
            var ruta   = Path.Combine(carpeta, nombre);

            using var stream = System.IO.File.Create(ruta);
            await archivo.CopyToAsync(stream);

            _db.ImagenesMovimiento.Add(new ImagenMovimiento
            {
                EquipoPerifericoId = epId,
                RutaImagen         = $"/uploads/movimientos/{nombre}",
                Descripcion        = i < descripciones.Count ? descripciones[i] : null,
                Orden              = orden++,
                FechaSubida        = DateTime.Now
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { mensaje = "Imágenes guardadas correctamente." });
    }

    // Generar PDF de hallazgos de un periférico asignado directamente
    public async Task<IActionResult> PdfHallazgosPeriferico(int epId)
    {
        if (!await Puede("perifericos.asignar")) return AccesoDenegado();

        var asignacion = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Empleado).ThenInclude(e => e!.Departamento)
            .Include(ep => ep.MiembroExterno)
            .Include(ep => ep.Grupo)
            .Include(ep => ep.Imagenes.OrderBy(i => i.Orden))
            .Include(ep => ep.Sitio)
            .FirstOrDefaultAsync(ep => ep.Id == epId);

        if (asignacion == null) return NotFound();
        if (!asignacion.Imagenes.Any())
        {
            TempData["Error"] = "Esta asignación no tiene imágenes adjuntas.";
            return RedirectToAction(nameof(Details), new { id = asignacion.PerifericoId });
        }

        var usuarioActual = await _users.GetUserAsync(User);
        var bytes = _pdf.GenerarPdfHallazgosPeriferico(asignacion, usuarioActual?.RutaFirmaIT);
        var nombre = $"Hallazgos_{asignacion.Periferico?.Marca}_{asignacion.Periferico?.Modelo}_{asignacion.FechaAsignacion:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    public async Task<IActionResult> DescargarCartaDirecta(int asignacionId)
    {
        var ep = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Empleado).ThenInclude(e => e!.Departamento)
            .Include(ep => ep.MiembroExterno)
            .Include(ep => ep.Grupo)
            .FirstOrDefaultAsync(ep => ep.Id == asignacionId);
        if (ep == null || ep.Periferico == null ||
            (ep.Empleado == null && ep.MiembroExterno == null && ep.Grupo == null))
            return NotFound();

        // Obtener firma del usuario logueado
        var usuarioActual = await _users.GetUserAsync(User);
        var rutaFirmaIT   = usuarioActual?.RutaFirmaIT;

        var bytes = _pdf.GenerarCartaCompromisoPerifericos(ep, rutaFirmaIT);
        var nombre = $"Carta_Periferico_{SanitizarNombreArchivo(ep.NombreResponsable)}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    private static string SanitizarNombreArchivo(string nombre) =>
        string.Join("_", nombre.Split(Path.GetInvalidFileNameChars().Append(' ').ToArray(), StringSplitOptions.RemoveEmptyEntries));
}
