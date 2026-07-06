using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class MovimientosController : BaseController
{
    private readonly AppDbContext _db;
    private readonly PdfService _pdf;
    private readonly UserManager<UsuarioApp> _users;
    public MovimientosController(AppDbContext db, PdfService pdf, UserManager<UsuarioApp> users, PermisoService permisos) : base(permisos)
    { _db = db; _pdf = pdf; _users = users; }

    private static readonly string[] ClavesMovimiento =
        ["movimientos.asignar", "movimientos.prestamo", "movimientos.devolucion", "movimientos.garantia", "equipos.baja"];

    private static string ClaveParaTipo(string? tipo) => tipo switch
    {
        "Asignacion"      => "movimientos.asignar",
        "Prestamo"        => "movimientos.prestamo",
        "Devolucion"      => "movimientos.devolucion",
        "EntradaGarantia" => "movimientos.garantia",
        "SalidaGarantia"  => "movimientos.garantia",
        "Baja"            => "equipos.baja",
        "Reactivacion"    => "equipos.baja",
        _                 => ""
    };

    public async Task<IActionResult> Registrar(int equipoId)
    {
        if (!await PuedeAlguno(ClavesMovimiento)) return AccesoDenegado();

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
                .OrderBy(e => e.Nombre).ToListAsync(),
            Sitios = await _db.Sitios.Where(s => s.Activo).OrderBy(s => s.Nombre).ToListAsync()
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ListarSitios()
    {
        var sitios = await _db.Sitios.Where(s => s.Activo).OrderBy(s => s.Nombre)
            .Select(s => new { s.Id, s.Nombre })
            .ToListAsync();
        return Json(sitios);
    }

    [HttpGet]
    public async Task<IActionResult> ListarSitiosInactivos()
    {
        if (!await Puede("sitios.eliminar")) return Forbid();
        var sitios = await _db.Sitios.Where(s => !s.Activo).OrderBy(s => s.Nombre)
            .Select(s => new { s.Id, s.Nombre })
            .ToListAsync();
        return Json(sitios);
    }

    [HttpPost]
    public async Task<IActionResult> NuevoSitio([FromBody] string nombre)
    {
        if (!await Puede("sitios.crear")) return Forbid();
        if (string.IsNullOrWhiteSpace(nombre)) return BadRequest("Nombre requerido.");
        var nombreLimpio = nombre.Trim();
        var existente = await _db.Sitios.FirstOrDefaultAsync(s => s.Nombre == nombreLimpio);
        if (existente != null)
        {
            if (!existente.Activo)
            {
                existente.Activo = true;
                await _db.SaveChangesAsync();
            }
            return Ok(new { id = existente.Id, nombre = existente.Nombre });
        }
        var sitio = new Sitio { Nombre = nombreLimpio };
        _db.Sitios.Add(sitio);
        await _db.SaveChangesAsync();
        return Ok(new { id = sitio.Id, nombre = sitio.Nombre });
    }

    [HttpDelete]
    public async Task<IActionResult> EliminarSitio(int id)
    {
        if (!await Puede("sitios.eliminar")) return Forbid();
        var sitio = await _db.Sitios.FindAsync(id);
        if (sitio == null) return NotFound();

        sitio.Activo = false;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> ReactivarSitio(int id)
    {
        if (!await Puede("sitios.eliminar")) return Forbid();
        var sitio = await _db.Sitios.FindAsync(id);
        if (sitio == null) return NotFound();

        sitio.Activo = true;
        await _db.SaveChangesAsync();
        return Ok(new { id = sitio.Id, nombre = sitio.Nombre });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(MovimientoCreateViewModel vm)
    {
        var clave = ClaveParaTipo(vm.TipoMovimiento);
        if (string.IsNullOrEmpty(clave) || !await Puede(clave)) return AccesoDenegado();

        var equipo = await _db.Equipos.FindAsync(vm.EquipoId);
        if (equipo == null) return NotFound();

        // Limpiar ModelState de campos complejos que no vienen del form
        ModelState.Remove("Equipo");
        ModelState.Remove("Empleados");
        ModelState.Remove("Sitios");
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

        bool esAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (!ModelState.IsValid)
        {
            if (esAjax)
            {
                var errores = ModelState.Where(kvp => kvp.Value!.Errors.Count > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new { errores });
            }
            vm.Equipo = await _db.Equipos.Include(e => e.TipoEquipo).FirstAsync(e => e.Id == vm.EquipoId);
            vm.Empleados = await _db.Empleados.Where(e => e.Activo)
                .Include(e => e.Departamento).OrderBy(e => e.Nombre).ToListAsync();
            vm.Sitios = await _db.Sitios.OrderBy(s => s.Nombre).ToListAsync();
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
            FirmaEmpleado    = vm.FirmaEmpleado,  // guardada en BD, no en TempData
            SitioId          = await Puede("movimientos.sitio") ? vm.SitioId : null
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
                        EmpleadoId = vm.EmpleadoId,
                        TipoMovimiento = vm.TipoMovimiento!,
                        FechaAsignacion = DateTime.Now,
                        FechaDevolucionEstimada = vm.FechaFinEstimada,
                        Observaciones = vm.Observaciones,
                        FirmaEmpleado = vm.FirmaEmpleado,
                        SitioId = await Puede("movimientos.sitio") ? vm.SitioId : null
                    });
                }
            }
            await _db.SaveChangesAsync();
        }

        TempData["OK"] = "Movimiento registrado correctamente.";

        // Carta de préstamo/asignación → va a Carta (descarga inmediata)
        // Devolución con empleado identificado → va al Finiquito TI
        // Resto → detalle del equipo
        string redirectUrl = (vm.TipoMovimiento == "Prestamo" || vm.TipoMovimiento == "Asignacion")
            ? Url.Action(nameof(Carta), new { id = movimiento.Id })!
            : (vm.TipoMovimiento == "Devolucion" && movimiento.EmpleadoId != null)
                ? Url.Action(nameof(Finiquito), new { movimientoId = movimiento.Id })!
                : Url.Action("Details", "Equipos", new { id = vm.EquipoId })!;

        if (esAjax)
            return Json(new { movimientoId = movimiento.Id, redirectUrl });

        return Redirect(redirectUrl);
    }

    // Subir imágenes para un movimiento (llamado via JS después de confirmar)
    [HttpPost]
    public async Task<IActionResult> SubirImagenes(int movimientoId,
        List<IFormFile> imagenes, List<string?> descripciones)
    {
        if (!await PuedeAlguno("movimientos.asignar", "movimientos.prestamo", "movimientos.devolucion")) return Forbid();

        var movimiento = await _db.Movimientos.FindAsync(movimientoId);
        if (movimiento == null) return NotFound();

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

            var nombre = $"mov_{movimientoId}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}{ext}";
            var ruta   = Path.Combine(carpeta, nombre);

            using var stream = System.IO.File.Create(ruta);
            await archivo.CopyToAsync(stream);

            _db.ImagenesMovimiento.Add(new ImagenMovimiento
            {
                MovimientoId = movimientoId,
                RutaImagen   = $"/uploads/movimientos/{nombre}",
                Descripcion  = i < descripciones.Count ? descripciones[i] : null,
                Orden        = orden++,
                FechaSubida  = DateTime.Now
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { mensaje = "Imágenes guardadas correctamente." });
    }

    // Eliminar una imagen específica
    [HttpDelete]
    public async Task<IActionResult> EliminarImagen(int id)
    {
        if (!await PuedeAlguno("movimientos.asignar", "movimientos.prestamo", "movimientos.devolucion")) return Forbid();

        var img = await _db.ImagenesMovimiento.FindAsync(id);
        if (img == null) return NotFound();

        var rutaFisica = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
            img.RutaImagen.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(rutaFisica))
            System.IO.File.Delete(rutaFisica);

        _db.ImagenesMovimiento.Remove(img);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // Generar PDF de hallazgos
    public async Task<IActionResult> PdfHallazgos(int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var movimiento = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(e => e!.TipoEquipo)
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .Include(m => m.Imagenes.OrderBy(i => i.Orden))
            .Include(m => m.Sitio)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movimiento == null) return NotFound();
        if (!movimiento.Imagenes.Any())
        {
            TempData["Error"] = "Este movimiento no tiene imágenes adjuntas.";
            return RedirectToAction("Details", "Equipos", new { id = movimiento.EquipoId });
        }

        var usuarioActual = await _users.GetUserAsync(User);
        var bytes = _pdf.GenerarPdfHallazgos(movimiento, usuarioActual?.RutaFirmaIT);
        var nombre = $"Hallazgos_{movimiento.Equipo?.NombreEquipo}_{movimiento.FechaInicio:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    public async Task<IActionResult> Carta(int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var movimiento = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(e => e!.TipoEquipo)
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movimiento == null) return NotFound();
        return View(movimiento);
    }

    public async Task<IActionResult> DescargarCarta(int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var movimiento = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(e => e!.TipoEquipo)
            .Include(m => m.Equipo).ThenInclude(e => e!.EquiposPerifericos.Where(ep => ep.FechaDesvinculacion == null))
                .ThenInclude(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movimiento == null || movimiento.Empleado == null) return NotFound();

        // Obtener firma del usuario logueado
        var usuarioActual = await _users.GetUserAsync(User);
        var rutaFirmaIT   = usuarioActual?.RutaFirmaIT;

        byte[] bytes = movimiento.TipoMovimiento == "Prestamo"
            ? _pdf.GenerarCartaPrestamo(movimiento, movimiento.FirmaEmpleado, rutaFirmaIT)
            : _pdf.GenerarCartaCompromiso(movimiento, movimiento.FirmaEmpleado, rutaFirmaIT);

        movimiento.CartaGenerada = true;
        await _db.SaveChangesAsync();

        var nombre = $"Carta_{movimiento.TipoMovimiento}_{movimiento.Empleado.CodigoEmpleado}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    public async Task<IActionResult> Finiquito(int movimientoId)
    {
        if (!await Puede("movimientos.finiquito")) return AccesoDenegado();

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
        string observaciones,
        string? telNumero, string? telMarca, string? telModelo, string? telImei,
        string? firmaEmpleado)
    {
        if (!await Puede("movimientos.finiquito")) return AccesoDenegado();

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

        // Obtener firma del usuario logueado
        var usuarioActual = await _users.GetUserAsync(User);
        var rutaFirmaIT   = usuarioActual?.RutaFirmaIT;

        var d = new FiniquitoData
        {
            RutaFirmaIT    = rutaFirmaIT,
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
            TelNumero      = telNumero ?? "",
            TelMarca       = telMarca ?? "",
            TelModelo      = telModelo ?? "",
            TelImei        = telImei ?? "",
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
