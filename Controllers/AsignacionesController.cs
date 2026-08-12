using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;

namespace InventarioTI.Controllers;

// Asignación completa: equipo + periféricos + licencias a una misma
// persona/miembro externo/grupo en un solo formulario. Cada cosa que se
// crea queda igual de independiente que si se hubiera hecho por separado
// (el equipo genera su propio Movimiento, cada periférico su propio
// EquipoPeriferico "Directo", cada licencia su propia LicenciaAsignacion)
// — esto es solo una forma más rápida de hacerlas juntas cuando coinciden
// en el tiempo (p.ej. onboarding de alguien nuevo).
//
// El "ancla" (tipo/id) es el punto de entrada: puede ser una persona
// (empleado/miembroExterno/grupo, como antes) o un ítem concreto
// (equipo/periferico/licencia) cuando se entra desde su propio Details.
// Cuando el ancla es un ítem, el responsable se elige en el formulario en
// vez de venir fijo.
public class AsignacionesController : BaseController
{
    private readonly AppDbContext _db;
    public AsignacionesController(AppDbContext db, PermisoService permisos) : base(permisos) => _db = db;

    private static readonly string[] ClavesAsignacion =
        ["movimientos.asignar", "movimientos.prestamo", "perifericos.asignar", "licencias.asignar"];

    private static readonly string[] TiposPersona = ["empleado", "miembroExterno", "grupo"];

    [HttpGet]
    public async Task<IActionResult> Nueva(string tipo, int id)
    {
        if (!await PuedeAlguno(ClavesAsignacion)) return AccesoDenegado();

        bool anclaEsPersona = TiposPersona.Contains(tipo);
        string nombreAncla;

        if (anclaEsPersona)
        {
            object? responsable = tipo switch
            {
                "empleado"       => await _db.Empleados.Include(e => e.Departamento).FirstOrDefaultAsync(e => e.Id == id),
                "miembroExterno" => await _db.MiembrosExternos.FirstOrDefaultAsync(m => m.Id == id),
                "grupo"          => await _db.Grupos.FirstOrDefaultAsync(g => g.Id == id),
                _ => null
            };
            if (responsable == null) return NotFound();
            nombreAncla = responsable switch
            {
                Empleado e       => e.Nombre,
                MiembroExterno m => m.Nombre,
                Grupo g          => g.Nombre,
                _ => ""
            };
        }
        else
        {
            ViewBag.Empleados = await _db.Empleados.Where(e => e.Activo)
                .Include(e => e.Departamento).OrderBy(e => e.Nombre).ToListAsync();
            ViewBag.MiembrosExternos = await _db.MiembrosExternos.Where(m => m.Activo).OrderBy(m => m.Nombre).ToListAsync();
            ViewBag.Grupos = await _db.Grupos.Where(g => g.Activo).OrderBy(g => g.Nombre).ToListAsync();

            switch (tipo)
            {
                case "equipo":
                    var equipoAncla = await _db.Equipos.Include(e => e.TipoEquipo).FirstOrDefaultAsync(e => e.Id == id);
                    if (equipoAncla == null || equipoAncla.Estado != "Bodega") return NotFound();
                    ViewBag.EquipoPreset = equipoAncla;
                    nombreAncla = equipoAncla.NombreEquipo;
                    break;
                case "periferico":
                    var perifAncla = await _db.Perifericos.Include(p => p.TipoPeriferico).FirstOrDefaultAsync(p => p.Id == id);
                    if (perifAncla == null || perifAncla.Estado != "Disponible") return NotFound();
                    ViewBag.PerifericoPreset = perifAncla;
                    nombreAncla = $"{perifAncla.Marca} {perifAncla.Modelo}";
                    break;
                case "licencia":
                    var licAncla = await _db.TiposLicencia.FirstOrDefaultAsync(t => t.Id == id && t.Activo);
                    if (licAncla == null) return NotFound();
                    ViewBag.LicenciaPreset = licAncla;
                    nombreAncla = licAncla.Nombre;
                    break;
                default:
                    return NotFound();
            }
        }

        ViewBag.Tipo = tipo;
        ViewBag.AnclaId = id;
        ViewBag.AnclaEsPersona = anclaEsPersona;
        ViewBag.NombreAncla = nombreAncla;

        ViewBag.PuedeEquipo      = await PuedeAlguno("movimientos.asignar", "movimientos.prestamo");
        ViewBag.PuedePeriferico  = await Puede("perifericos.asignar");
        ViewBag.PuedeLicencia    = await Puede("licencias.asignar");
        ViewBag.PuedeSitio       = await Puede("movimientos.sitio");
        ViewBag.PuedeCrearSitio     = await Puede("sitios.crear");
        ViewBag.PuedeEliminarSitio  = await Puede("sitios.eliminar");

        if ((bool)ViewBag.PuedeEquipo && tipo != "equipo")
        {
            ViewBag.EquiposDisponibles = await _db.Equipos
                .Include(e => e.TipoEquipo)
                .Where(e => e.Estado == "Bodega")
                .OrderBy(e => e.NombreEquipo)
                .ToListAsync();
        }
        if ((bool)ViewBag.PuedeSitio)
        {
            ViewBag.Sitios = await _db.Sitios.Where(s => s.Activo).OrderBy(s => s.Nombre).ToListAsync();
        }

        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nueva(string tipo, int id,
        int? equipoId, string? tipoMovimiento, DateTime? fechaFinEstimada,
        string? perifericosIds, string? licenciasIds,
        string? observaciones, string? firmaEmpleado, int? sitioId,
        string? TipoResponsable, int? EmpleadoId, int? MiembroExternoId, int? GrupoId)
    {
        if (!await PuedeAlguno(ClavesAsignacion)) return AccesoDenegado();

        bool esAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        bool anclaEsPersona = TiposPersona.Contains(tipo);
        int? empleadoId, miembroExternoId, grupoId;
        bool existeResponsable;

        if (anclaEsPersona)
        {
            empleadoId = tipo == "empleado" ? id : null;
            miembroExternoId = tipo == "miembroExterno" ? id : null;
            grupoId = tipo == "grupo" ? id : null;
            existeResponsable = tipo switch
            {
                "empleado"       => await _db.Empleados.AnyAsync(e => e.Id == id),
                "miembroExterno" => await _db.MiembrosExternos.AnyAsync(m => m.Id == id),
                "grupo"          => await _db.Grupos.AnyAsync(g => g.Id == id),
                _ => false
            };
        }
        else
        {
            empleadoId = TipoResponsable == "Empleado" ? EmpleadoId : null;
            miembroExternoId = TipoResponsable == "MiembroExterno" ? MiembroExternoId : null;
            grupoId = TipoResponsable == "Grupo" ? GrupoId : null;
            existeResponsable =
                (empleadoId != null && await _db.Empleados.AnyAsync(e => e.Id == empleadoId)) ||
                (miembroExternoId != null && await _db.MiembrosExternos.AnyAsync(m => m.Id == miembroExternoId)) ||
                (grupoId != null && await _db.Grupos.AnyAsync(g => g.Id == grupoId));
        }
        if (!existeResponsable)
        {
            const string errorResponsable = "Selecciona un responsable válido para la asignación.";
            if (esAjax) return BadRequest(new { error = errorResponsable });
            TempData["Error"] = errorResponsable;
            return RedirectToAction(nameof(Nueva), new { tipo, id });
        }

        var perifIds = (perifericosIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        var licIds   = (licenciasIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

        if (equipoId == null && perifIds.Count == 0 && licIds.Count == 0)
        {
            const string errorSeleccion = "Selecciona al menos un equipo, periférico o licencia para asignar.";
            if (esAjax) return BadRequest(new { error = errorSeleccion });
            TempData["Error"] = errorSeleccion;
            return RedirectToAction(nameof(Nueva), new { tipo, id });
        }

        string tipoMov = tipoMovimiento == "Prestamo" ? "Prestamo" : "Asignacion";
        int? sitioAplicado = await Puede("movimientos.sitio") ? sitioId : null;
        var ahora = DateTime.Now;

        Movimiento? movimiento = null;
        var perifericosAsignados = new List<EquipoPeriferico>();
        var licenciasAsignadas   = new List<LicenciaAsignacion>();

        // ── Equipo ──
        if (equipoId != null && await PuedeAlguno("movimientos.asignar", "movimientos.prestamo"))
        {
            var equipo = await _db.Equipos.FindAsync(equipoId.Value);
            if (equipo != null && equipo.Estado == "Bodega")
            {
                equipo.Estado = tipoMov == "Prestamo" ? "Prestamo" : "Asignado";
                movimiento = new Movimiento
                {
                    EquipoId         = equipo.Id,
                    EmpleadoId       = empleadoId,
                    MiembroExternoId = miembroExternoId,
                    GrupoId          = grupoId,
                    TipoMovimiento   = tipoMov,
                    FechaInicio      = ahora,
                    FechaFinEstimada = tipoMov == "Prestamo" ? fechaFinEstimada : null,
                    Observaciones    = observaciones,
                    FirmaEmpleado    = firmaEmpleado,
                    SitioId          = sitioAplicado,
                    CreadoPorUsuarioId = UsuarioActualId
                };
                _db.Movimientos.Add(movimiento);
            }
        }

        // ── Periféricos ──
        if (perifIds.Count > 0 && await Puede("perifericos.asignar"))
        {
            var perifericos = await _db.Perifericos.Where(p => perifIds.Contains(p.Id) && p.Estado == "Disponible").ToListAsync();
            foreach (var p in perifericos)
            {
                p.Estado = "Asignado";
                var ep = new EquipoPeriferico
                {
                    EquipoId                = null,
                    PerifericoId            = p.Id,
                    EmpleadoId              = empleadoId,
                    MiembroExternoId        = miembroExternoId,
                    GrupoId                 = grupoId,
                    TipoAsignacion          = "Directo",
                    TipoMovimiento          = tipoMov,
                    FechaAsignacion         = ahora,
                    FechaDevolucionEstimada = tipoMov == "Prestamo" ? fechaFinEstimada : null,
                    Observaciones           = observaciones,
                    FirmaEmpleado           = firmaEmpleado,
                    SitioId                 = sitioAplicado,
                    CreadoPorUsuarioId      = UsuarioActualId
                };
                _db.EquiposPerifericos.Add(ep);
                perifericosAsignados.Add(ep);
            }
        }

        // ── Licencias ──
        if (licIds.Count > 0 && await Puede("licencias.asignar"))
        {
            var tipos = await _db.TiposLicencia.Where(t => licIds.Contains(t.Id) && t.Activo).ToListAsync();
            foreach (var t in tipos)
            {
                var la = new LicenciaAsignacion
                {
                    TipoLicenciaId   = t.Id,
                    EmpleadoId       = empleadoId,
                    MiembroExternoId = miembroExternoId,
                    GrupoId          = grupoId,
                    TipoAsignacion   = "Directo",
                    TipoMovimiento   = "Asignacion",
                    FechaAsignacion  = ahora,
                    Observaciones    = observaciones,
                    CreadoPorUsuarioId = UsuarioActualId
                };
                _db.LicenciasAsignaciones.Add(la);
                licenciasAsignadas.Add(la);
            }
        }

        await _db.SaveChangesAsync();

        TempData["OK"] = "Asignación completa registrada correctamente.";

        var redirectUrl = Url.Action(nameof(Confirmacion), new
        {
            movimientoId = movimiento?.Id,
            perifericoAsignacionIds = string.Join(',', perifericosAsignados.Select(ep => ep.Id)),
            licenciasCount = licenciasAsignadas.Count,
            tipo, id
        })!;

        if (esAjax) return Json(new { movimientoId = movimiento?.Id, redirectUrl });
        return Redirect(redirectUrl);
    }

    public async Task<IActionResult> Confirmacion(int? movimientoId, string? perifericoAsignacionIds, int licenciasCount, string tipo, int id)
    {
        if (!await PuedeAlguno(ClavesAsignacion)) return AccesoDenegado();

        ViewBag.Tipo = tipo;
        ViewBag.AnclaId = id;

        if (movimientoId != null)
        {
            ViewBag.Movimiento = await _db.Movimientos
                .Include(m => m.Equipo)
                .FirstOrDefaultAsync(m => m.Id == movimientoId);
        }

        var perifIds = (perifericoAsignacionIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        ViewBag.Perifericos = perifIds.Count == 0 ? [] : await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Where(ep => perifIds.Contains(ep.Id))
            .ToListAsync();

        ViewBag.LicenciasCount = licenciasCount;

        return View();
    }
}
