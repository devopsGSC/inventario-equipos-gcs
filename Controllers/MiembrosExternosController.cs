using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class MiembrosExternosController : BaseController
{
    private readonly AppDbContext _db;
    private readonly PdfService _pdf;
    private readonly UserManager<UsuarioApp> _users;
    public MiembrosExternosController(AppDbContext db, PdfService pdf, UserManager<UsuarioApp> users, PermisoService permisos) : base(permisos)
    { _db = db; _pdf = pdf; _users = users; }

    public async Task<IActionResult> Index(bool? activo, string? buscar, int pagina = 1)
    {
        if (!await Puede("miembrosexternos.ver")) return AccesoDenegado();

        const int tamPagina = 12;
        var query = _db.MiembrosExternos.AsQueryable();

        if (activo.HasValue)
            query = query.Where(m => m.Activo == activo.Value);
        if (!string.IsNullOrEmpty(buscar))
            query = query.Where(m => m.Nombre.Contains(buscar) ||
                                     (m.Organizacion != null && m.Organizacion.Contains(buscar)) ||
                                     (m.Identificacion != null && m.Identificacion.Contains(buscar)));

        var total = await query.CountAsync();
        var miembros = await query
            .OrderBy(m => m.Nombre)
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
        return View(miembros);
    }

    public async Task<IActionResult> Details(int id, int pagina = 1)
    {
        if (!await Puede("miembrosexternos.detalle")) return AccesoDenegado();

        const int tamPagina = 8;
        var miembro = await _db.MiembrosExternos.FirstOrDefaultAsync(m => m.Id == id);
        if (miembro == null) return NotFound();

        var equiposActuales = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => m.MiembroExternoId == id && m.FechaDevolucion == null &&
                        (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
            .ToListAsync();
        ViewBag.EquiposActuales = equiposActuales;

        ViewBag.PerifsActuales = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Equipo)
            .Where(ep => ep.MiembroExternoId == id && ep.FechaDesvinculacion == null)
            .OrderByDescending(ep => ep.FechaAsignacion)
            .ToListAsync();

        ViewBag.HistorialPerifericos = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Equipo)
            .Where(ep => ep.MiembroExternoId == id && ep.TipoMovimiento != "Devolucion")
            .OrderByDescending(ep => ep.FechaAsignacion)
            .ToListAsync();

        ViewBag.LicenciasActuales = await _db.LicenciasAsignaciones
            .Include(la => la.TipoLicencia)
            .Include(la => la.Equipo)
            .Where(la => la.MiembroExternoId == id && la.FechaDesvinculacion == null)
            .OrderByDescending(la => la.FechaAsignacion)
            .ToListAsync();

        ViewBag.HistorialLicencias = await _db.LicenciasAsignaciones
            .Include(la => la.TipoLicencia)
            .Include(la => la.Equipo)
            .Where(la => la.MiembroExternoId == id && la.TipoMovimiento != "Devolucion")
            .OrderByDescending(la => la.FechaAsignacion)
            .ToListAsync();

        var totalHistorial = await _db.Movimientos.CountAsync(m => m.MiembroExternoId == id);
        ViewBag.Historial = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => m.MiembroExternoId == id)
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
        return View(miembro);
    }

    // Carta con TODOS los equipos y periféricos que tiene actualmente el
    // miembro externo, para no tener que generar una por cada equipo.
    public async Task<IActionResult> DescargarCartaGeneral(int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var miembro = await _db.MiembrosExternos.FirstOrDefaultAsync(m => m.Id == id);
        if (miembro == null) return NotFound();

        var equiposActuales = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => m.MiembroExternoId == id && m.FechaDevolucion == null &&
                        (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
            .ToListAsync();

        var perifsActuales = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Equipo)
            .Where(ep => ep.MiembroExternoId == id && ep.FechaDesvinculacion == null)
            .OrderByDescending(ep => ep.FechaAsignacion)
            .ToListAsync();

        var usuarioActual = await _users.GetUserAsync(User);

        var data = new CartaGeneralData
        {
            Colaborador    = miembro.Nombre,
            Centro         = miembro.Organizacion ?? "",
            Area           = miembro.Referencia ?? "",
            CodEmpleado    = "N/A",
            Identificacion = miembro.Identificacion ?? "",
            NombreEmisor   = usuarioActual?.NombreCompleto,
            RutaFirmaIT    = usuarioActual?.RutaFirmaIT,
            Equipos = equiposActuales.Select(m => new EquipoResumenItem
            {
                Tipo        = m.Equipo?.TipoEquipo?.Nombre ?? "",
                Marca       = m.Equipo?.Marca ?? "",
                Modelo      = m.Equipo?.Modelo ?? "",
                NumeroSerie = m.Equipo?.NumeroSerie ?? "",
                Movimiento  = m.TipoMovimiento,
                Desde       = m.FechaInicio.ToString("dd/MM/yyyy")
            }).ToList(),
            Perifericos = perifsActuales.Select(ep => new PerifericoResumenItem
            {
                Tipo        = ep.Periferico?.TipoPeriferico?.Nombre ?? "",
                Marca       = ep.Periferico?.Marca ?? "",
                Modelo      = ep.Periferico?.Modelo ?? "",
                NumeroSerie = ep.Periferico?.NumeroSerie ?? "",
                ViaEquipo   = ep.Equipo?.NombreEquipo ?? "Directo"
            }).ToList()
        };

        var bytes = _pdf.GenerarCartaGeneral(data);
        var nombre = $"Carta_General_{SanitizarNombreArchivo(miembro.Nombre)}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    private static string SanitizarNombreArchivo(string nombre) =>
        string.Join("_", nombre.Split(Path.GetInvalidFileNameChars().Append(' ').ToArray(), StringSplitOptions.RemoveEmptyEntries));

    public async Task<IActionResult> Create()
    {
        if (!await Puede("miembrosexternos.crear")) return AccesoDenegado();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MiembroExterno miembro)
    {
        if (!await Puede("miembrosexternos.crear")) return AccesoDenegado();

        if (!ModelState.IsValid)
            return View(miembro);

        miembro.FechaRegistro = DateTime.Now;
        miembro.CreadoPorUsuarioId = UsuarioActualId;
        _db.MiembrosExternos.Add(miembro);
        await _db.SaveChangesAsync();
        TempData["OK"] = "Miembro externo registrado correctamente.";
        return RedirectToAction(nameof(Details), new { id = miembro.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        if (!await Puede("miembrosexternos.editar")) return AccesoDenegado();

        var miembro = await _db.MiembrosExternos.FindAsync(id);
        if (miembro == null) return NotFound();
        return View(miembro);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MiembroExterno miembro)
    {
        if (!await Puede("miembrosexternos.editar")) return AccesoDenegado();

        if (id != miembro.Id) return BadRequest();
        if (!ModelState.IsValid)
            return View(miembro);

        var original = await _db.MiembrosExternos.FindAsync(id);
        if (original == null) return NotFound();
        original.Nombre = miembro.Nombre;
        original.Organizacion = miembro.Organizacion;
        original.Identificacion = miembro.Identificacion;
        original.Referencia = miembro.Referencia;
        original.Activo = miembro.Activo;
        await _db.SaveChangesAsync();
        TempData["OK"] = "Miembro externo actualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
