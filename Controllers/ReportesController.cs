using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.Text;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class ReportesController : BaseController
{
    private readonly AppDbContext _db;
    private readonly PdfService   _pdf;

    public ReportesController(AppDbContext db, PdfService pdf, PermisoService permisos) : base(permisos)
    { _db = db; _pdf = pdf; }

    private async Task<bool> PuedeVerOExportar(string? formato) =>
        string.IsNullOrEmpty(formato) ? await Puede("reportes.ver") : await Puede("reportes.exportar");

    // ─── ÍNDICE ───────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index() => await Puede("reportes.ver") ? View() : AccesoDenegado();

    // ═════════════════════════════════════════════════════════════════════════
    // EQUIPOS
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<IActionResult> Equipos(
        string? buscar, string? tipoEquipo, string? estadoEquipo,
        string? marcaEquipo, string? departamento, string? empleadoId,
        string? miembroExternoId, string? grupoId,
        string? sitio,
        DateTime? fechaCompraDesde, DateTime? fechaCompraHasta,
        DateTime? fechaGarantiaDesde, DateTime? fechaGarantiaHasta,
        bool? garantiaVencida, bool? garantiaPorVencer, int? garantiaDias,
        bool? sinAsignar, int? diasEnBodega,
        int pagina = 1, string? formato = null)
    {
        if (!await PuedeVerOExportar(formato)) return AccesoDenegado();

        var query = _db.Equipos.Include(e => e.TipoEquipo).Include(e => e.PlanData).AsQueryable();

        if (!string.IsNullOrEmpty(buscar))
            query = query.Where(e => e.NombreEquipo.Contains(buscar) ||
                                     e.NumeroSerie.Contains(buscar) ||
                                     e.Marca.Contains(buscar) ||
                                     e.Modelo.Contains(buscar));
        if (!string.IsNullOrEmpty(tipoEquipo))
            query = query.Where(e => e.TipoEquipo!.Nombre == tipoEquipo);
        if (!string.IsNullOrEmpty(estadoEquipo))
            query = query.Where(e => e.Estado == estadoEquipo);
        if (!string.IsNullOrEmpty(marcaEquipo))
            query = query.Where(e => e.Marca.Contains(marcaEquipo));
        if (fechaCompraDesde.HasValue)
            query = query.Where(e => e.FechaCompra != null && e.FechaCompra >= fechaCompraDesde);
        if (fechaCompraHasta.HasValue)
            query = query.Where(e => e.FechaCompra != null && e.FechaCompra <= fechaCompraHasta);
        if (fechaGarantiaDesde.HasValue)
            query = query.Where(e => e.FechaGarantia != null && e.FechaGarantia >= fechaGarantiaDesde);
        if (fechaGarantiaHasta.HasValue)
            query = query.Where(e => e.FechaGarantia != null && e.FechaGarantia <= fechaGarantiaHasta);
        if (garantiaVencida == true)
            query = query.Where(e => e.FechaGarantia != null && e.FechaGarantia.Value < DateTime.Today);
        if (garantiaPorVencer == true)
        {
            var limite = DateTime.Today.AddDays(garantiaDias ?? 30);
            query = query.Where(e => e.FechaGarantia != null &&
                                      e.FechaGarantia.Value >= DateTime.Today &&
                                      e.FechaGarantia.Value <= limite);
        }
        if (sinAsignar == true)
        {
            var corte = DateTime.Today.AddDays(-(diasEnBodega ?? 0));
            query = diasEnBodega.HasValue
                ? query.Where(e => e.Estado == "Bodega" && e.FechaRegistro <= corte)
                : query.Where(e => e.Estado == "Bodega");
        }

        // Filtros que requieren subquery sobre movimientos
        if (!string.IsNullOrEmpty(empleadoId) && int.TryParse(empleadoId, out int empIdEq))
        {
            var idsEmp = await _db.Movimientos
                .Where(m => m.EmpleadoId == empIdEq &&
                            m.FechaDevolucion == null &&
                            (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
                .Select(m => m.EquipoId).ToListAsync();
            query = query.Where(e => idsEmp.Contains(e.Id));
        }
        if (!string.IsNullOrEmpty(departamento))
        {
            var idsDept = await _db.Movimientos
                .Include(m => m.Empleado).ThenInclude(emp => emp!.Departamento)
                .Where(m => m.FechaDevolucion == null &&
                            (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo") &&
                            m.Empleado!.Departamento!.Nombre == departamento)
                .Select(m => m.EquipoId).Distinct().ToListAsync();
            query = query.Where(e => idsDept.Contains(e.Id));
        }
        if (!string.IsNullOrEmpty(miembroExternoId) && int.TryParse(miembroExternoId, out int miembroIdEq))
        {
            var idsMiembro = await _db.Movimientos
                .Where(m => m.MiembroExternoId == miembroIdEq &&
                            m.FechaDevolucion == null &&
                            (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
                .Select(m => m.EquipoId).ToListAsync();
            query = query.Where(e => idsMiembro.Contains(e.Id));
        }
        if (!string.IsNullOrEmpty(grupoId) && int.TryParse(grupoId, out int grupoIdEq))
        {
            var idsGrupo = await _db.Movimientos
                .Where(m => m.GrupoId == grupoIdEq &&
                            m.FechaDevolucion == null &&
                            (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
                .Select(m => m.EquipoId).ToListAsync();
            query = query.Where(e => idsGrupo.Contains(e.Id));
        }
        if (!string.IsNullOrEmpty(sitio))
        {
            var idsConSitio = await _db.Movimientos
                .Where(m => m.Sitio!.Nombre == sitio &&
                            m.FechaDevolucion == null &&
                            (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
                .Select(m => m.EquipoId)
                .ToListAsync();
            query = query.Where(e => idsConSitio.Contains(e.Id));
        }

        var equipos    = await query.OrderBy(e => e.TipoEquipo!.Nombre).ThenBy(e => e.NombreEquipo).ToListAsync();
        var equipoIds  = equipos.Select(e => e.Id).ToList();

        var movsActivos = (await _db.Movimientos
            .Include(m => m.Empleado).ThenInclude(emp => emp!.Departamento)
            .Include(m => m.MiembroExterno)
            .Include(m => m.Grupo)
            .Include(m => m.Sitio)
            .Where(m => equipoIds.Contains(m.EquipoId) &&
                        m.FechaDevolucion == null &&
                        (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
            .ToListAsync())
            .GroupBy(m => m.EquipoId).ToDictionary(g => g.Key, g => g.First());

        if (formato == "excel") return ExcelEquipos(equipos, movsActivos);
        if (formato == "csv")   return CsvEquipos(equipos, movsActivos);
        if (formato == "pdf")   return PdfEquipos(equipos, movsActivos);

        const int tam  = 20;
        var total      = equipos.Count;
        var paginaDatos = equipos.Skip((pagina - 1) * tam).Take(tam).ToList();

        ViewBag.MovsActivos        = movsActivos;
        ViewBag.Buscar             = buscar;
        ViewBag.TipoEquipo         = tipoEquipo;
        ViewBag.EstadoEquipo       = estadoEquipo;
        ViewBag.MarcaEquipo        = marcaEquipo;
        ViewBag.Departamento       = departamento;
        ViewBag.EmpleadoId         = empleadoId;
        ViewBag.MiembroExternoId   = miembroExternoId;
        ViewBag.GrupoId            = grupoId;
        ViewBag.SitioFiltro        = sitio;
        ViewBag.FechaCompraDesde   = fechaCompraDesde?.ToString("yyyy-MM-dd");
        ViewBag.FechaCompraHasta   = fechaCompraHasta?.ToString("yyyy-MM-dd");
        ViewBag.FechaGarantiaDesde = fechaGarantiaDesde?.ToString("yyyy-MM-dd");
        ViewBag.FechaGarantiaHasta = fechaGarantiaHasta?.ToString("yyyy-MM-dd");
        ViewBag.GarantiaVencida    = garantiaVencida;
        ViewBag.GarantiaPorVencer  = garantiaPorVencer;
        ViewBag.GarantiaDias       = garantiaDias ?? 30;
        ViewBag.SinAsignar         = sinAsignar;
        ViewBag.DiasEnBodega       = diasEnBodega;
        ViewBag.TotalSinPaginar    = total;
        ViewBag.Paginacion = new PaginacionViewModel
        {
            PaginaActual = pagina, TotalPaginas = (int)Math.Ceiling(total / (double)tam),
            TotalRegistros = total, TamañoPagina = tam
        };
        ViewBag.TiposEquipo   = await _db.TiposEquipo
            .Where(t => t.Nombre != "Desktop" && t.Nombre != "Monitor" && t.Nombre != "Impresora")
            .OrderBy(t => t.Nombre).Select(t => t.Nombre).ToListAsync();
        ViewBag.Marcas        = await _db.Equipos.Select(e => e.Marca).Distinct().OrderBy(m => m).ToListAsync();
        ViewBag.Departamentos = await _db.Departamentos.OrderBy(d => d.Nombre).Select(d => d.Nombre).ToListAsync();
        ViewBag.Empleados     = await _db.Empleados.Where(e => e.Activo)
            .OrderBy(e => e.Nombre).Select(e => new { e.Id, e.Nombre, e.CodigoEmpleado }).ToListAsync();
        ViewBag.MiembrosExternos = await _db.MiembrosExternos.Where(m => m.Activo)
            .OrderBy(m => m.Nombre).Select(m => new { m.Id, m.Nombre }).ToListAsync();
        ViewBag.Grupos        = await _db.Grupos.Where(g => g.Activo)
            .OrderBy(g => g.Nombre).Select(g => new { g.Id, g.Nombre }).ToListAsync();
        ViewBag.Sitios        = await _db.Sitios.Where(s => s.Activo).OrderBy(s => s.Nombre).Select(s => s.Nombre).ToListAsync();

        return View(paginaDatos);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PERIFÉRICOS
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<IActionResult> Perifericos(
        string? buscar, string? tipoPeriferico, string? estadoPeriferico,
        string? marcaPeriferico, string? empleadoId,
        string? miembroExternoId, string? grupoId,
        DateTime? fechaCompraDesde, DateTime? fechaCompraHasta,
        int pagina = 1, string? formato = null)
    {
        if (!await PuedeVerOExportar(formato)) return AccesoDenegado();

        var query = _db.Perifericos.Include(p => p.TipoPeriferico).AsQueryable();

        if (!string.IsNullOrEmpty(buscar))
            query = query.Where(p => p.Marca.Contains(buscar) ||
                                     p.Modelo.Contains(buscar) ||
                                     p.NumeroSerie.Contains(buscar));
        if (!string.IsNullOrEmpty(tipoPeriferico))
            query = query.Where(p => p.TipoPeriferico!.Nombre == tipoPeriferico);
        if (!string.IsNullOrEmpty(estadoPeriferico))
            query = query.Where(p => p.Estado == estadoPeriferico);
        if (!string.IsNullOrEmpty(marcaPeriferico))
            query = query.Where(p => p.Marca.Contains(marcaPeriferico));
        if (fechaCompraDesde.HasValue)
            query = query.Where(p => p.FechaCompra != null && p.FechaCompra >= fechaCompraDesde);
        if (fechaCompraHasta.HasValue)
            query = query.Where(p => p.FechaCompra != null && p.FechaCompra <= fechaCompraHasta);

        if (!string.IsNullOrEmpty(empleadoId) && int.TryParse(empleadoId, out int empIdPf))
        {
            var equiposDel = await _db.Movimientos
                .Where(m => m.EmpleadoId == empIdPf && m.FechaDevolucion == null &&
                            (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
                .Select(m => m.EquipoId).ToListAsync();
            var perPorEq = await _db.EquiposPerifericos
                .Where(ep => equiposDel.Contains(ep.EquipoId ?? 0) && ep.FechaDesvinculacion == null)
                .Select(ep => ep.PerifericoId).ToListAsync();
            var perDirect = await _db.EquiposPerifericos
                .Where(ep => ep.EmpleadoId == empIdPf && ep.FechaDesvinculacion == null)
                .Select(ep => ep.PerifericoId).ToListAsync();
            var todosPf = perPorEq.Union(perDirect).ToList();
            query = query.Where(p => todosPf.Contains(p.Id));
        }
        if (!string.IsNullOrEmpty(miembroExternoId) && int.TryParse(miembroExternoId, out int miembroIdPf))
        {
            var equiposDel = await _db.Movimientos
                .Where(m => m.MiembroExternoId == miembroIdPf && m.FechaDevolucion == null &&
                            (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
                .Select(m => m.EquipoId).ToListAsync();
            var perPorEq = await _db.EquiposPerifericos
                .Where(ep => equiposDel.Contains(ep.EquipoId ?? 0) && ep.FechaDesvinculacion == null)
                .Select(ep => ep.PerifericoId).ToListAsync();
            var perDirect = await _db.EquiposPerifericos
                .Where(ep => ep.MiembroExternoId == miembroIdPf && ep.FechaDesvinculacion == null)
                .Select(ep => ep.PerifericoId).ToListAsync();
            var todosPf = perPorEq.Union(perDirect).ToList();
            query = query.Where(p => todosPf.Contains(p.Id));
        }
        if (!string.IsNullOrEmpty(grupoId) && int.TryParse(grupoId, out int grupoIdPf))
        {
            var equiposDel = await _db.Movimientos
                .Where(m => m.GrupoId == grupoIdPf && m.FechaDevolucion == null &&
                            (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
                .Select(m => m.EquipoId).ToListAsync();
            var perPorEq = await _db.EquiposPerifericos
                .Where(ep => equiposDel.Contains(ep.EquipoId ?? 0) && ep.FechaDesvinculacion == null)
                .Select(ep => ep.PerifericoId).ToListAsync();
            var perDirect = await _db.EquiposPerifericos
                .Where(ep => ep.GrupoId == grupoIdPf && ep.FechaDesvinculacion == null)
                .Select(ep => ep.PerifericoId).ToListAsync();
            var todosPf = perPorEq.Union(perDirect).ToList();
            query = query.Where(p => todosPf.Contains(p.Id));
        }

        var perifericos = await query.OrderBy(p => p.TipoPeriferico!.Nombre).ThenBy(p => p.Marca).ToListAsync();
        var perifIds    = perifericos.Select(p => p.Id).ToList();

        var asignActivas = (await _db.EquiposPerifericos
            .Include(ep => ep.Equipo)
            .Include(ep => ep.Empleado).ThenInclude(emp => emp!.Departamento)
            .Include(ep => ep.MiembroExterno)
            .Include(ep => ep.Grupo)
            .Where(ep => perifIds.Contains(ep.PerifericoId) && ep.FechaDesvinculacion == null)
            .ToListAsync())
            .GroupBy(ep => ep.PerifericoId).ToDictionary(g => g.Key, g => g.First());

        if (formato == "excel") return ExcelPerifericos(perifericos, asignActivas);
        if (formato == "csv")   return CsvPerifericos(perifericos, asignActivas);
        if (formato == "pdf")   return PdfPerifericos(perifericos, asignActivas);

        const int tam  = 20;
        var total      = perifericos.Count;

        ViewBag.AsignActivas    = asignActivas;
        ViewBag.Buscar          = buscar;
        ViewBag.TipoPeriferico  = tipoPeriferico;
        ViewBag.EstadoPeriferico= estadoPeriferico;
        ViewBag.MarcaPeriferico = marcaPeriferico;
        ViewBag.EmpleadoId      = empleadoId;
        ViewBag.MiembroExternoId = miembroExternoId;
        ViewBag.GrupoId         = grupoId;
        ViewBag.FechaCompraDesde= fechaCompraDesde?.ToString("yyyy-MM-dd");
        ViewBag.FechaCompraHasta= fechaCompraHasta?.ToString("yyyy-MM-dd");
        ViewBag.TotalSinPaginar = total;
        ViewBag.Paginacion = new PaginacionViewModel
        {
            PaginaActual = pagina, TotalPaginas = (int)Math.Ceiling(total / (double)tam),
            TotalRegistros = total, TamañoPagina = tam
        };
        ViewBag.TiposPeriferico = await _db.TiposPerifericos
            .Where(t => t.Nombre != "Otro").OrderBy(t => t.Nombre).Select(t => t.Nombre).ToListAsync();
        ViewBag.Marcas    = await _db.Perifericos.Select(p => p.Marca).Distinct().OrderBy(m => m).ToListAsync();
        ViewBag.Empleados = await _db.Empleados.Where(e => e.Activo)
            .OrderBy(e => e.Nombre).Select(e => new { e.Id, e.Nombre, e.CodigoEmpleado }).ToListAsync();
        ViewBag.MiembrosExternos = await _db.MiembrosExternos.Where(m => m.Activo)
            .OrderBy(m => m.Nombre).Select(m => new { m.Id, m.Nombre }).ToListAsync();
        ViewBag.Grupos    = await _db.Grupos.Where(g => g.Activo)
            .OrderBy(g => g.Nombre).Select(g => new { g.Id, g.Nombre }).ToListAsync();

        return View(perifericos.Skip((pagina - 1) * tam).Take(tam).ToList());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // EMPLEADOS
    // ═════════════════════════════════════════════════════════════════════════
    public async Task<IActionResult> Empleados(
        string? buscar, string? departamento, string? empleadoId,
        bool? soloActivos, bool? soloConEquipos, bool? soloSinEquipos,
        bool? multiplesEquipos, int? minimoEquipos,
        string? tipoMovimiento,
        DateTime? fechaAsignacionDesde, DateTime? fechaAsignacionHasta,
        int pagina = 1, string? formato = null)
    {
        if (!await PuedeVerOExportar(formato)) return AccesoDenegado();

        var query = _db.Empleados.Include(e => e.Departamento).AsQueryable();

        if (!string.IsNullOrEmpty(buscar))
            query = query.Where(e => e.Nombre.Contains(buscar) ||
                                     e.CodigoEmpleado.Contains(buscar) ||
                                     e.DUI.Contains(buscar));
        if (!string.IsNullOrEmpty(departamento))
            query = query.Where(e => e.Departamento!.Nombre == departamento);
        if (!string.IsNullOrEmpty(empleadoId) && int.TryParse(empleadoId, out int empIdE))
            query = query.Where(e => e.Id == empIdE);
        if (soloActivos == true)  query = query.Where(e => e.Activo);
        if (soloActivos == false) query = query.Where(e => !e.Activo);

        var empleados = await query.OrderBy(e => e.Departamento!.Nombre).ThenBy(e => e.Nombre).ToListAsync();
        var empIds    = empleados.Select(e => e.Id).ToList();

        // Movimientos activos con filtros de tipo y fecha en DB
        var movsQ = _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => empIds.Contains(m.EmpleadoId ?? 0) &&
                        m.FechaDevolucion == null &&
                        (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"));
        if (!string.IsNullOrEmpty(tipoMovimiento))
            movsQ = movsQ.Where(m => m.TipoMovimiento == tipoMovimiento);
        if (fechaAsignacionDesde.HasValue)
            movsQ = movsQ.Where(m => m.FechaInicio >= fechaAsignacionDesde.Value);
        if (fechaAsignacionHasta.HasValue)
            movsQ = movsQ.Where(m => m.FechaInicio <= fechaAsignacionHasta.Value);

        var movs       = await movsQ.ToListAsync();
        var movsPorEmp = movs.GroupBy(m => m.EmpleadoId ?? 0).ToDictionary(g => g.Key, g => g.ToList());

        // Periféricos directos
        var perifsDir  = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Where(ep => empIds.Contains(ep.EmpleadoId ?? 0) && ep.FechaDesvinculacion == null)
            .ToListAsync();
        var perifsPorEmp = perifsDir.GroupBy(ep => ep.EmpleadoId ?? 0).ToDictionary(g => g.Key, g => g.ToList());

        // Filtros en memoria sobre conteo de equipos
        if (soloConEquipos == true)
            empleados = empleados.Where(e => movsPorEmp.ContainsKey(e.Id)).ToList();
        if (soloSinEquipos == true)
            empleados = empleados.Where(e => !movsPorEmp.ContainsKey(e.Id)).ToList();
        if (multiplesEquipos == true)
            empleados = empleados.Where(e => (movsPorEmp.GetValueOrDefault(e.Id)?.Count ?? 0) > 1).ToList();
        if (minimoEquipos.HasValue && minimoEquipos > 0)
            empleados = empleados.Where(e => (movsPorEmp.GetValueOrDefault(e.Id)?.Count ?? 0) >= minimoEquipos.Value).ToList();

        if (formato == "excel") return ExcelEmpleados(empleados, movsPorEmp, perifsPorEmp);
        if (formato == "csv")   return CsvEmpleados(empleados, movsPorEmp, perifsPorEmp);
        if (formato == "pdf")   return PdfEmpleados(empleados, movsPorEmp, perifsPorEmp);

        const int tam  = 20;
        var total      = empleados.Count;

        ViewBag.MovsPorEmp       = movsPorEmp;
        ViewBag.PerifsDir        = perifsPorEmp;
        ViewBag.Buscar           = buscar;
        ViewBag.Departamento     = departamento;
        ViewBag.EmpleadoId       = empleadoId;
        ViewBag.SoloActivos      = soloActivos;
        ViewBag.SoloConEquipos   = soloConEquipos;
        ViewBag.SoloSinEquipos   = soloSinEquipos;
        ViewBag.MultiplesEquipos = multiplesEquipos;
        ViewBag.MinimoEquipos    = minimoEquipos;
        ViewBag.TipoMovimiento   = tipoMovimiento;
        ViewBag.FechaAsignDesde  = fechaAsignacionDesde?.ToString("yyyy-MM-dd");
        ViewBag.FechaAsignHasta  = fechaAsignacionHasta?.ToString("yyyy-MM-dd");
        ViewBag.TotalSinPaginar  = total;
        ViewBag.Paginacion = new PaginacionViewModel
        {
            PaginaActual = pagina, TotalPaginas = (int)Math.Ceiling(total / (double)tam),
            TotalRegistros = total, TamañoPagina = tam
        };
        ViewBag.Departamentos = await _db.Departamentos.OrderBy(d => d.Nombre).Select(d => d.Nombre).ToListAsync();
        ViewBag.Empleados     = await _db.Empleados.Where(e => e.Activo)
            .OrderBy(e => e.Nombre).Select(e => new { e.Id, e.Nombre, e.CodigoEmpleado }).ToListAsync();

        return View(empleados.Skip((pagina - 1) * tam).Take(tam).ToList());
    }

    // ─── EXPORT: EQUIPOS ──────────────────────────────────────────────────────

    private static string[] EncabezadosEquipos => new[]
        { "Nombre", "Tipo", "Marca", "Modelo", "Serie", "IMEI", "Estado",
          "RAM", "Procesador", "Almacenamiento", "Plan de datos",
          "Accesorios", "Responsable", "Departamento / Organización", "Sitio", "Tipo movimiento",
          "Fecha asignación", "Fecha compra", "Garantía" };

    private static List<string[]> FilasEquipos(List<Equipo> equipos, Dictionary<int, Movimiento> movs) =>
        equipos.Select(e => {
            var m = movs.GetValueOrDefault(e.Id);
            return new[] {
                e.NombreEquipo, e.TipoEquipo?.Nombre ?? "", e.Marca, e.Modelo,
                e.NumeroSerie, e.IMEI ?? "", e.Estado,
                e.RAM ?? "—", e.Procesador ?? "—", e.Almacenamiento ?? "—", e.PlanData?.Nombre ?? "—",
                e.Accesorios ?? "",
                m?.NombreResponsable ?? "—",
                m?.Empleado?.Departamento?.Nombre ?? m?.MiembroExterno?.Organizacion ?? m?.Grupo?.Descripcion ?? "—",
                m?.Sitio?.Nombre ?? "—",
                m?.TipoMovimiento ?? "—", m?.FechaInicio.ToString("dd/MM/yyyy") ?? "—",
                e.FechaCompra?.ToString("dd/MM/yyyy") ?? "—",
                e.FechaGarantia?.ToString("dd/MM/yyyy") ?? "—"
            };
        }).ToList();

    private IActionResult ExcelEquipos(List<Equipo> equipos, Dictionary<int, Movimiento> movs)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Equipos");
        EscribirEncabezados(ws, EncabezadosEquipos);
        EscribirFilas(ws, FilasEquipos(equipos, movs));
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream(); wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Reporte_Equipos_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    private IActionResult CsvEquipos(List<Equipo> equipos, Dictionary<int, Movimiento> movs) =>
        CsvFile(EncabezadosEquipos, FilasEquipos(equipos, movs), "Equipos");

    private IActionResult PdfEquipos(List<Equipo> equipos, Dictionary<int, Movimiento> movs)
    {
        var filas = FilasEquipos(equipos, movs).Select(f => (IEnumerable<string>)f);
        return File(_pdf.GenerarReportePdf("Inventario de Equipos", EncabezadosEquipos.ToList(), filas, ""),
            "application/pdf", $"Reporte_Equipos_{DateTime.Now:yyyyMMdd}.pdf");
    }

    // ─── EXPORT: PERIFÉRICOS ──────────────────────────────────────────────────

    private static string[] EncabezadosPerifericos => new[]
        { "Tipo", "Marca", "Modelo", "Serie", "Estado",
          "Equipo adjunto", "Responsable", "Departamento / Organización", "Fecha compra", "Registrado" };

    private static List<string[]> FilasPerifericos(List<Periferico> perifericos,
        Dictionary<int, EquipoPeriferico> asignActivas) =>
        perifericos.Select(p => {
            asignActivas.TryGetValue(p.Id, out var ep);
            string equipo, empleado, depto;
            if (ep == null) { equipo = empleado = depto = "—"; }
            else
            {
                equipo   = ep.Equipo?.NombreEquipo ?? "—";
                empleado = ep.NombreResponsable;
                depto    = ep.Empleado?.Departamento?.Nombre ?? ep.MiembroExterno?.Organizacion ?? ep.Grupo?.Descripcion ?? "—";
            }
            return new[] {
                p.TipoPeriferico?.Nombre ?? "", p.Marca, p.Modelo, p.NumeroSerie,
                p.Estado, equipo, empleado, depto,
                p.FechaCompra?.ToString("dd/MM/yyyy") ?? "—",
                p.FechaRegistro.ToString("dd/MM/yyyy")
            };
        }).ToList();

    private IActionResult ExcelPerifericos(List<Periferico> perifericos,
        Dictionary<int, EquipoPeriferico> asignActivas)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Periféricos");
        EscribirEncabezados(ws, EncabezadosPerifericos);
        EscribirFilas(ws, FilasPerifericos(perifericos, asignActivas));
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream(); wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Reporte_Perifericos_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    private IActionResult CsvPerifericos(List<Periferico> perifericos,
        Dictionary<int, EquipoPeriferico> asignActivas) =>
        CsvFile(EncabezadosPerifericos, FilasPerifericos(perifericos, asignActivas), "Perifericos");

    private IActionResult PdfPerifericos(List<Periferico> perifericos,
        Dictionary<int, EquipoPeriferico> asignActivas)
    {
        var filas = FilasPerifericos(perifericos, asignActivas).Select(f => (IEnumerable<string>)f);
        return File(_pdf.GenerarReportePdf("Inventario de Periféricos", EncabezadosPerifericos.ToList(), filas, ""),
            "application/pdf", $"Reporte_Perifericos_{DateTime.Now:yyyyMMdd}.pdf");
    }

    // ─── EXPORT: EMPLEADOS ────────────────────────────────────────────────────

    private static string[] EncabezadosEmpleados => new[]
        { "Empleado", "Código", "DUI", "Departamento", "Cargo", "Estado",
          "Tipo mov.", "Equipo", "Tipo equipo", "Serie equipo", "Fecha asignación" };

    private static List<string[]> FilasEmpleados(
        List<Empleado> empleados,
        Dictionary<int, List<Movimiento>> movsPorEmp,
        Dictionary<int, List<EquipoPeriferico>> perifsPorEmp)
    {
        var resultado = new List<string[]>();
        foreach (var emp in empleados)
        {
            var movs   = movsPorEmp.GetValueOrDefault(emp.Id) ?? [];
            var estado = emp.Activo ? "Activo" : "Inactivo";
            if (!movs.Any())
            {
                resultado.Add(new[] {
                    emp.Nombre, emp.CodigoEmpleado, emp.DUI,
                    emp.Departamento?.Nombre ?? "", emp.Cargo, estado,
                    "—", "Sin asignaciones", "", "", ""
                });
            }
            else
            {
                foreach (var mov in movs)
                    resultado.Add(new[] {
                        emp.Nombre, emp.CodigoEmpleado, emp.DUI,
                        emp.Departamento?.Nombre ?? "", emp.Cargo, estado,
                        mov.TipoMovimiento,
                        mov.Equipo?.NombreEquipo ?? "",
                        mov.Equipo?.TipoEquipo?.Nombre ?? "",
                        mov.Equipo?.NumeroSerie ?? "",
                        mov.FechaInicio.ToString("dd/MM/yyyy")
                    });
            }
        }
        return resultado;
    }

    private IActionResult ExcelEmpleados(List<Empleado> empleados,
        Dictionary<int, List<Movimiento>> movsPorEmp,
        Dictionary<int, List<EquipoPeriferico>> perifsPorEmp)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Asignaciones");
        EscribirEncabezados(ws, EncabezadosEmpleados);
        EscribirFilas(ws, FilasEmpleados(empleados, movsPorEmp, perifsPorEmp));
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream(); wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Reporte_Asignaciones_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    private IActionResult CsvEmpleados(List<Empleado> empleados,
        Dictionary<int, List<Movimiento>> movsPorEmp,
        Dictionary<int, List<EquipoPeriferico>> perifsPorEmp) =>
        CsvFile(EncabezadosEmpleados, FilasEmpleados(empleados, movsPorEmp, perifsPorEmp), "Asignaciones");

    private IActionResult PdfEmpleados(List<Empleado> empleados,
        Dictionary<int, List<Movimiento>> movsPorEmp,
        Dictionary<int, List<EquipoPeriferico>> perifsPorEmp)
    {
        var filas = FilasEmpleados(empleados, movsPorEmp, perifsPorEmp).Select(f => (IEnumerable<string>)f);
        return File(_pdf.GenerarReportePdf("Asignaciones por Empleado", EncabezadosEmpleados.ToList(), filas, ""),
            "application/pdf", $"Reporte_Asignaciones_{DateTime.Now:yyyyMMdd}.pdf");
    }

    // ─── Helpers de escritura Excel/CSV ──────────────────────────────────────

    private static void EscribirEncabezados(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#374151");
            cell.Style.Font.FontColor = XLColor.White;
        }
    }

    private static void EscribirFilas(IXLWorksheet ws, List<string[]> filas)
    {
        for (int r = 0; r < filas.Count; r++)
            for (int c = 0; c < filas[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = filas[r][c];
    }

    private IActionResult CsvFile(string[] headers, List<string[]> filas, string nombre)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));
        foreach (var fila in filas)
            sb.AppendLine(string.Join(",", fila.Select(v => $"\"{v.Replace("\"", "\"\"")}\"")));
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
            $"Reporte_{nombre}_{DateTime.Now:yyyyMMdd}.csv");
    }
}
