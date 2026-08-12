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

    private static string SanitizarNombreArchivo(string nombre) =>
        string.Join("_", nombre.Split(Path.GetInvalidFileNameChars().Append(' ').ToArray(), StringSplitOptions.RemoveEmptyEntries));

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

    public async Task<IActionResult> Registrar(int equipoId, int? empleadoId = null, int? miembroExternoId = null, int? grupoId = null)
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
            TipoResponsable = miembroExternoId.HasValue ? "MiembroExterno" : grupoId.HasValue ? "Grupo" : "Empleado",
            EmpleadoId = empleadoId,
            MiembroExternoId = miembroExternoId,
            GrupoId = grupoId,
            Empleados = await _db.Empleados.Where(e => e.Activo)
                .Include(e => e.Departamento)
                .OrderBy(e => e.Nombre).ToListAsync(),
            MiembrosExternos = await _db.MiembrosExternos.Where(m => m.Activo).OrderBy(m => m.Nombre).ToListAsync(),
            Grupos = await _db.Grupos.Where(g => g.Activo).OrderBy(g => g.Nombre).ToListAsync(),
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
        ModelState.Remove("MiembrosExternos");
        ModelState.Remove("Grupos");
        ModelState.Remove("Sitios");
        ModelState.Remove("EmpleadoId");
        ModelState.Remove("MiembroExternoId");
        ModelState.Remove("GrupoId");
        ModelState.Remove("FechaFinEstimada");
        ModelState.Remove("FechaInicio");

        if (vm.FechaInicio == default)
            vm.FechaInicio = DateTime.Now;

        bool requiereResponsable = vm.TipoMovimiento == "Asignacion" || vm.TipoMovimiento == "Prestamo";

        // Validaciones manuales
        if (string.IsNullOrEmpty(vm.TipoMovimiento))
            ModelState.AddModelError("TipoMovimiento", "Seleccione un tipo de movimiento.");

        if (requiereResponsable)
        {
            bool tieneResponsable = vm.TipoResponsable switch
            {
                "MiembroExterno" => vm.MiembroExternoId != null,
                "Grupo"          => vm.GrupoId != null,
                _                => vm.EmpleadoId != null
            };
            if (!tieneResponsable)
                ModelState.AddModelError("EmpleadoId", "Debe seleccionar un responsable.");
        }

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
            vm.MiembrosExternos = await _db.MiembrosExternos.Where(m => m.Activo).OrderBy(m => m.Nombre).ToListAsync();
            vm.Grupos = await _db.Grupos.Where(g => g.Activo).OrderBy(g => g.Nombre).ToListAsync();
            vm.Sitios = await _db.Sitios.OrderBy(s => s.Nombre).ToListAsync();
            return View(vm);
        }

        // Cerrar movimiento activo si es devolución o salida de garantía
        // *** Sin .Contains() en la query EF — comparaciones explícitas ***
        int? empleadoAnteriorId = null;
        int? miembroExternoAnteriorId = null;
        int? grupoAnteriorId = null;
        if (vm.TipoMovimiento == "Devolucion" || vm.TipoMovimiento == "SalidaGarantia")
        {
            // OrderByDescending es imprescindible: si por algún motivo quedó
            // más de un movimiento abierto para este equipo (no debería
            // pasar, pero ya pasó), sin esto se cierra uno arbitrario en vez
            // del más reciente, y la devolución queda atribuida a la persona
            // equivocada.
            var activo = await _db.Movimientos
                .Where(m => m.EquipoId == vm.EquipoId &&
                    m.FechaDevolucion == null &&
                    (m.TipoMovimiento == "Asignacion" ||
                     m.TipoMovimiento == "Prestamo" ||
                     m.TipoMovimiento == "EntradaGarantia"))
                .OrderByDescending(m => m.FechaInicio)
                .FirstOrDefaultAsync();

            if (activo != null)
            {
                activo.FechaDevolucion = DateTime.Now;
                // guardar para el finiquito
                empleadoAnteriorId = activo.EmpleadoId;
                miembroExternoAnteriorId = activo.MiembroExternoId;
                grupoAnteriorId = activo.GrupoId;
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

        int? nuevoEmpleadoId = null, nuevoMiembroExternoId = null, nuevoGrupoId = null;
        if (requiereResponsable)
        {
            nuevoEmpleadoId = vm.TipoResponsable == "Empleado" ? vm.EmpleadoId : null;
            nuevoMiembroExternoId = vm.TipoResponsable == "MiembroExterno" ? vm.MiembroExternoId : null;
            nuevoGrupoId = vm.TipoResponsable == "Grupo" ? vm.GrupoId : null;
        }
        else if (vm.TipoMovimiento == "Devolucion")
        {
            nuevoEmpleadoId = empleadoAnteriorId;
            nuevoMiembroExternoId = miembroExternoAnteriorId;
            nuevoGrupoId = grupoAnteriorId;
        }

        var movimiento = new Movimiento
        {
            EquipoId         = vm.EquipoId,
            EmpleadoId       = nuevoEmpleadoId,
            MiembroExternoId = nuevoMiembroExternoId,
            GrupoId          = nuevoGrupoId,
            TipoMovimiento   = vm.TipoMovimiento!,
            FechaInicio      = vm.FechaInicio,
            FechaFinEstimada = vm.FechaFinEstimada,
            Observaciones    = vm.Observaciones,
            FirmaEmpleado    = vm.FirmaEmpleado,  // guardada en BD, no en TempData
            SitioId          = await Puede("movimientos.sitio") ? vm.SitioId : null,
            CreadoPorUsuarioId = UsuarioActualId
        };
        _db.Movimientos.Add(movimiento);
        await _db.SaveChangesAsync();

        TempData["OK"] = "Movimiento registrado correctamente.";

        // Carta de préstamo/asignación → va a Carta (descarga inmediata)
        // Devolución con responsable identificado → va al Finiquito TI
        // Resto → detalle del equipo
        bool tieneResponsableFinal = movimiento.EmpleadoId != null || movimiento.MiembroExternoId != null || movimiento.GrupoId != null;
        string redirectUrl = (vm.TipoMovimiento == "Prestamo" || vm.TipoMovimiento == "Asignacion")
            ? Url.Action(nameof(Carta), new { id = movimiento.Id })!
            : (vm.TipoMovimiento == "Devolucion" && tieneResponsableFinal)
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
        var omitidas = new List<string>();
        var extensionesValidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        for (int i = 0; i < imagenes.Count; i++)
        {
            var archivo = imagenes[i];
            if (archivo.Length == 0) continue;
            if (archivo.Length > 10 * 1024 * 1024) { omitidas.Add($"{archivo.FileName} (supera 10 MB)"); continue; }

            var ext = Path.GetExtension(archivo.FileName).ToLower();
            if (!extensionesValidas.Contains(ext))
            {
                omitidas.Add($"{archivo.FileName} (formato no soportado, usa JPG/PNG/WEBP)");
                continue;
            }

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
        return Ok(new { mensaje = "Imágenes guardadas correctamente.", omitidas });
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
            .Include(m => m.MiembroExterno)
            .Include(m => m.Grupo)
            .Include(m => m.Imagenes.OrderBy(i => i.Orden))
            .Include(m => m.Sitio)
            .Include(m => m.CreadoPorUsuario)
            .Include(m => m.EntregadoPorUsuario)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movimiento == null) return NotFound();
        if (!movimiento.Imagenes.Any())
        {
            TempData["Error"] = "Este movimiento no tiene imágenes adjuntas.";
            return RedirectToAction("Details", "Equipos", new { id = movimiento.EquipoId });
        }

        // La firma de IT debe ser de quien realmente entregó/hizo el
        // movimiento, no de quien descarga el PDF después. Se prioriza a
        // quien se marcó como "entregado por" en la carta (puede ser
        // distinto de quien registró el movimiento), y para movimientos
        // viejos sin ninguno de esos datos se usa el usuario actual.
        var usuarioActual = await _users.GetUserAsync(User);
        var usuarioEmisor = movimiento.EntregadoPorUsuario ?? movimiento.CreadoPorUsuario ?? usuarioActual;
        var bytes = _pdf.GenerarPdfHallazgos(movimiento, usuarioEmisor?.RutaFirmaIT);
        var nombre = $"Hallazgos_{movimiento.Equipo?.NombreEquipo}_{movimiento.FechaInicio:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    public async Task<IActionResult> Carta(int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var movimiento = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(e => e!.TipoEquipo)
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .Include(m => m.MiembroExterno)
            .Include(m => m.Grupo)
            .Include(m => m.CreadoPorUsuario)
            .Include(m => m.EntregadoPorUsuario)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movimiento == null) return NotFound();
        return View(movimiento);
    }

    // Marca la entrega física como completada (fija quién entrega y agrega
    // su nota a las observaciones) y vuelve a la pantalla de la carta, que
    // ya se recarga mostrando el estado "Entregado" y el botón de descarga.
    // Separado de DescargarCarta porque esa acción devuelve el PDF como
    // archivo adjunto: el navegador no navega ni refresca la página, así
    // que si el estado cambiara ahí, la vista se quedaría desactualizada.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarEntrega(int id, string? notaEntrega)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var movimiento = await _db.Movimientos.FirstOrDefaultAsync(m => m.Id == id);
        if (movimiento == null) return NotFound();

        var usuarioActual = await _users.GetUserAsync(User);
        if (!movimiento.EntregaCompletada && usuarioActual != null)
        {
            movimiento.EntregadoPorUsuarioId = usuarioActual.Id;
            movimiento.EntregaCompletada     = true;
            movimiento.FechaEntrega          = DateTime.Now;
            var nota = string.IsNullOrWhiteSpace(notaEntrega) ? "Equipo entregado." : notaEntrega.Trim();
            movimiento.Observaciones = AgregarNotaObservacion(movimiento.Observaciones,
                usuarioActual.NombreCompleto ?? usuarioActual.Email ?? "Usuario", nota);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Carta), new { id });
    }

    // Agrega una línea con fecha y usuario al final de las observaciones,
    // sin perder las notas anteriores (p.ej. la de quien asignó el equipo,
    // seguida más tarde por la de quien lo entrega físicamente).
    private static string AgregarNotaObservacion(string? actual, string usuario, string nota)
    {
        var linea = $"[{DateTime.Now:dd/MM/yyyy HH:mm}] {usuario}: {nota}";
        return string.IsNullOrWhiteSpace(actual) ? linea : $"{actual}\n{linea}";
    }

    // Genera (o reutiliza si sigue vigente) un link de un solo uso para que
    // el colaborador firme el movimiento a distancia cuando no está
    // físicamente presente para firmar en el acto.
    public async Task<IActionResult> GenerarLinkFirma(int id)
    {
        if (!await PuedeAlguno(ClavesMovimiento)) return AccesoDenegado();

        var movimiento = await _db.Movimientos
            .Include(m => m.Equipo)
            .Include(m => m.Empleado)
            .Include(m => m.MiembroExterno)
            .Include(m => m.Grupo)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movimiento == null) return NotFound();

        if (!string.IsNullOrEmpty(movimiento.FirmaEmpleado))
        {
            TempData["Error"] = "Este movimiento ya tiene una firma registrada.";
            return RedirectToAction(nameof(Carta), new { id });
        }

        var solicitud = await _db.SolicitudesFirma
            .Where(s => s.MovimientoId == id && s.FechaFirmado == null && s.FechaExpiracion >= DateTime.Now)
            .OrderByDescending(s => s.FechaCreacion)
            .FirstOrDefaultAsync();

        if (solicitud == null)
        {
            solicitud = new SolicitudFirma
            {
                MovimientoId    = id,
                Token           = TokenGenerator.GenerarTokenUrlSafe(),
                FechaCreacion   = DateTime.Now,
                FechaExpiracion = DateTime.Now.AddDays(3)
            };
            _db.SolicitudesFirma.Add(solicitud);
            await _db.SaveChangesAsync();
        }

        ViewBag.Link = Url.Action("Firmar", "FirmaRemota", new { token = solicitud.Token }, Request.Scheme);
        ViewBag.Movimiento = movimiento;
        return View(solicitud);
    }

    public async Task<IActionResult> DescargarCarta(int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var movimiento = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(e => e!.TipoEquipo)
            .Include(m => m.Equipo).ThenInclude(e => e!.PlanData)
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .Include(m => m.MiembroExterno)
            .Include(m => m.Grupo)
            .Include(m => m.CreadoPorUsuario)
            .Include(m => m.EntregadoPorUsuario)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movimiento == null || (movimiento.Empleado == null && movimiento.MiembroExterno == null && movimiento.Grupo == null))
            return NotFound();

        var usuarioActual = await _users.GetUserAsync(User);

        // Quién entrega el equipo y firma por TI puede ser distinto de quien
        // registró la asignación en el sistema. Esa atribución la fija
        // explícitamente MarcarEntrega (el usuario la marca a mano); descargar
        // la carta (para revisarla, reimprimirla, etc.) no debe darla por
        // entregada por sí sola. Mientras no se haya marcado, se cae en
        // CreadoPorUsuarioId y, en última instancia, en el usuario actual.
        var usuarioEmisor = movimiento.EntregadoPorUsuario ?? movimiento.CreadoPorUsuario ?? usuarioActual;
        var rutaFirmaIT   = usuarioEmisor?.RutaFirmaIT;

        // Periféricos asignados a la misma persona/miembro externo/grupo de
        // este movimiento, para que la carta del equipo también los incluya.
        var perifericos = await _db.EquiposPerifericos
            .Where(ep => ep.FechaDesvinculacion == null &&
                ((movimiento.EmpleadoId != null && ep.EmpleadoId == movimiento.EmpleadoId) ||
                 (movimiento.MiembroExternoId != null && ep.MiembroExternoId == movimiento.MiembroExternoId) ||
                 (movimiento.GrupoId != null && ep.GrupoId == movimiento.GrupoId)))
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .ToListAsync();

        byte[] bytes = movimiento.TipoMovimiento == "Prestamo"
            ? _pdf.GenerarCartaPrestamo(movimiento, movimiento.FirmaEmpleado, rutaFirmaIT, usuarioEmisor?.NombreCompleto, perifericos)
            : _pdf.GenerarCartaCompromiso(movimiento, movimiento.FirmaEmpleado, rutaFirmaIT, usuarioEmisor?.NombreCompleto, perifericos);

        movimiento.CartaGenerada = true;
        await _db.SaveChangesAsync();

        var nombre = $"Carta_{movimiento.TipoMovimiento}_{SanitizarNombreArchivo(movimiento.NombreResponsable)}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    public async Task<IActionResult> Finiquito(int movimientoId)
    {
        if (!await Puede("movimientos.finiquito")) return AccesoDenegado();

        var mov = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(e => e!.TipoEquipo)
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .Include(m => m.MiembroExterno)
            .Include(m => m.Grupo)
            .FirstOrDefaultAsync(m => m.Id == movimientoId);
        if (mov == null) return NotFound();

        // Buscar el movimiento anterior (asignación/préstamo) para precargar datos
        var movAnterior = await _db.Movimientos
            .Include(m => m.Empleado).ThenInclude(e => e!.Departamento)
            .Include(m => m.MiembroExterno)
            .Include(m => m.Grupo)
            .Where(m => m.EquipoId == mov.EquipoId &&
                        m.Id != movimientoId &&
                        (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
            .OrderByDescending(m => m.FechaInicio)
            .FirstOrDefaultAsync();

        // Periféricos que tiene actualmente asignados esta persona/grupo —
        // al generar el finiquito se devuelven junto con el equipo, porque
        // implica que la persona deja la empresa (ver DescargarFiniquito).
        var perifsActuales = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Where(ep => ep.FechaDesvinculacion == null &&
                ((mov.EmpleadoId != null && ep.EmpleadoId == mov.EmpleadoId) ||
                 (mov.MiembroExternoId != null && ep.MiembroExternoId == mov.MiembroExternoId) ||
                 (mov.GrupoId != null && ep.GrupoId == mov.GrupoId)))
            .ToListAsync();

        ViewBag.MovAnterior      = movAnterior;
        ViewBag.PerifsDevueltos  = perifsActuales;
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
            .Include(m => m.MiembroExterno)
            .Include(m => m.Grupo)
            .FirstOrDefaultAsync(m => m.Id == movimientoId);
        if (mov == null || (mov.Empleado == null && mov.MiembroExterno == null && mov.Grupo == null))
            return NotFound();

        var eq = mov.Equipo!;

        // El finiquito implica que la persona deja la empresa: sus
        // periféricos actualmente asignados se devuelven junto con el
        // equipo en este mismo momento.
        var perifsActuales = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Where(ep => ep.FechaDesvinculacion == null &&
                ((mov.EmpleadoId != null && ep.EmpleadoId == mov.EmpleadoId) ||
                 (mov.MiembroExternoId != null && ep.MiembroExternoId == mov.MiembroExternoId) ||
                 (mov.GrupoId != null && ep.GrupoId == mov.GrupoId)))
            .ToListAsync();

        var ahoraFiniquito = DateTime.Now;
        foreach (var ep in perifsActuales)
        {
            ep.FechaDesvinculacion = ahoraFiniquito;
            if (ep.Periferico != null) ep.Periferico.Estado = "Disponible";
        }

        // Obtener firma del usuario logueado
        var usuarioActual = await _users.GetUserAsync(User);
        var rutaFirmaIT   = usuarioActual?.RutaFirmaIT;

        var d = new FiniquitoData
        {
            RutaFirmaIT    = rutaFirmaIT,
            Fecha          = mov.FechaInicio.ToString("dd/MMM/yyyy"),
            Colaborador    = mov.NombreResponsable,
            Centro         = mov.Empleado?.Departamento?.Nombre ?? mov.MiembroExterno?.Organizacion ?? mov.Grupo?.Descripcion ?? "",
            Area           = mov.Empleado?.Cargo ?? mov.MiembroExterno?.Referencia ?? "",
            CodEmpleado    = mov.Empleado?.CodigoEmpleado ?? "N/A",
            Identificacion = mov.Empleado?.DUI ?? mov.MiembroExterno?.Identificacion ?? "",
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
            Perifericos    = perifsActuales.Select(ep => new PerifericoFiniquito
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

        var nombre = $"Finiquito_TI_{SanitizarNombreArchivo(mov.NombreResponsable)}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }
}
