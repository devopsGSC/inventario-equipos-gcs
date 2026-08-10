using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Filters;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class HomeController : Controller
{
    [AllowAnonymous, NoTrackNavigation]
    public IActionResult Error() => View();

    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? serie)
    {
        var vm = new DashboardViewModel
        {
            TotalEquipos    = await _db.Equipos.CountAsync(),
            EnBodega        = await _db.Equipos.CountAsync(e => e.Estado == "Bodega"),
            Asignados       = await _db.Equipos.CountAsync(e => e.Estado == "Asignado"),
            EnPrestamo      = await _db.Equipos.CountAsync(e => e.Estado == "Prestamo"),
            EnGarantia      = await _db.Equipos.CountAsync(e => e.Estado == "EnGarantia"),
            EnBaja          = await _db.Equipos.CountAsync(e => e.Estado == "Baja"),
            GarantiasProximas = await _db.Equipos.CountAsync(e =>
                e.FechaGarantia.HasValue &&
                e.FechaGarantia.Value >= DateTime.Today &&
                e.FechaGarantia.Value <= DateTime.Today.AddDays(30)),
            GarantiasVencidas = await _db.Equipos.CountAsync(e =>
                e.FechaGarantia.HasValue &&
                e.FechaGarantia.Value < DateTime.Today &&
                e.Estado != "Baja"),
        };
        vm.ActividadReciente = await ObtenerActividadReciente();

        if (!string.IsNullOrWhiteSpace(serie))
        {
            vm.SerieBuscada = serie;
            vm.ResultadoBusqueda = await _db.Equipos
                .Include(e => e.TipoEquipo)
                .Include(e => e.Movimientos.Where(m => m.FechaDevolucion == null &&
                    (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo" || m.TipoMovimiento == "EntradaGarantia")))
                    .ThenInclude(m => m.Empleado).ThenInclude(emp => emp!.Departamento)
                .Include(e => e.Movimientos.Where(m => m.FechaDevolucion == null &&
                    (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo" || m.TipoMovimiento == "EntradaGarantia")))
                    .ThenInclude(m => m.MiembroExterno)
                .Include(e => e.Movimientos.Where(m => m.FechaDevolucion == null &&
                    (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo" || m.TipoMovimiento == "EntradaGarantia")))
                    .ThenInclude(m => m.Grupo)
                .FirstOrDefaultAsync(e => e.NumeroSerie == serie);
        }

        return View(vm);
    }

    // Feed unificado de "Actividad reciente": junta movimientos de equipos,
    // registros/asignaciones de periféricos y licencias, y altas de
    // personas (empleados, miembros externos, grupos) en una sola línea de
    // tiempo, con quién hizo cada acción. Se trae un puñado reciente de
    // cada fuente por separado (mucho más liviano que un UNION real contra
    // tablas con formas distintas) y se combina/ordena en memoria.
    private async Task<List<ActividadItem>> ObtenerActividadReciente()
    {
        const int porFuente = 15;
        const int totalFeed = 15;
        var items = new List<ActividadItem>();

        var movimientos = await _db.Movimientos
            .Include(m => m.Equipo)
            .Include(m => m.Empleado)
            .Include(m => m.MiembroExterno)
            .Include(m => m.Grupo)
            .Include(m => m.CreadoPorUsuario)
            .OrderByDescending(m => m.FechaInicio)
            .Take(porFuente)
            .ToListAsync();
        foreach (var m in movimientos)
        {
            var (icono, badge, titulo) = m.TipoMovimiento switch
            {
                "Asignacion"      => ("bi-person-check-fill", "badge-asignado", "Asignación de equipo"),
                "Prestamo"        => ("bi-arrow-left-right",  "badge-prestamo", "Préstamo de equipo"),
                "Devolucion"      => ("bi-arrow-return-left", "badge-green",    "Devolución de equipo"),
                "EntradaGarantia" => ("bi-shield-check",      "badge-garantia", "Entrada a garantía"),
                "SalidaGarantia"  => ("bi-shield-x",          "badge-green",    "Salida de garantía"),
                "Baja"            => ("bi-x-circle-fill",     "badge-baja",     "Baja de equipo"),
                "Reactivacion"    => ("bi-arrow-counterclockwise", "badge-green", "Reactivación de equipo"),
                _                 => ("bi-circle", "badge-bodega", m.TipoMovimiento)
            };
            var responsable = m.Empleado != null || m.MiembroExterno != null || m.Grupo != null ? m.NombreResponsable : null;
            items.Add(new ActividadItem
            {
                Fecha = m.FechaInicio, Icono = icono, BadgeClase = badge, Titulo = titulo,
                Descripcion = m.Equipo?.NombreEquipo ?? "",
                Responsable = responsable,
                Usuario = m.CreadoPorUsuario?.NombreCompleto,
                LinkController = "Equipos", LinkAction = "Details", LinkId = m.EquipoId
            });
        }

        var equipos = await _db.Equipos
            .Include(e => e.TipoEquipo)
            .Include(e => e.CreadoPorUsuario)
            .OrderByDescending(e => e.FechaRegistro)
            .Take(porFuente)
            .ToListAsync();
        items.AddRange(equipos.Select(e => new ActividadItem
        {
            Fecha = e.FechaRegistro, Icono = "bi-laptop", BadgeClase = "badge-bodega", Titulo = "Equipo registrado",
            Descripcion = e.NombreEquipo, Usuario = e.CreadoPorUsuario?.NombreCompleto,
            LinkController = "Equipos", LinkAction = "Details", LinkId = e.Id
        }));

        var perifericos = await _db.Perifericos
            .Include(p => p.TipoPeriferico)
            .Include(p => p.CreadoPorUsuario)
            .OrderByDescending(p => p.FechaRegistro)
            .Take(porFuente)
            .ToListAsync();
        items.AddRange(perifericos.Select(p => new ActividadItem
        {
            Fecha = p.FechaRegistro, Icono = "bi-mouse2", BadgeClase = "badge-bodega", Titulo = "Periférico registrado",
            Descripcion = $"{p.TipoPeriferico?.Nombre} — {p.Marca} {p.Modelo}", Usuario = p.CreadoPorUsuario?.NombreCompleto,
            LinkController = "Perifericos", LinkAction = "Details", LinkId = p.Id
        }));

        // Solo asignaciones directas (sin equipo): las vía equipo ya quedan
        // representadas por el movimiento del equipo, mostrarlas aparte
        // duplicaría la misma acción dos veces en el feed.
        var perifericosDirectos = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico)
            .Include(ep => ep.CreadoPorUsuario)
            .Where(ep => ep.TipoAsignacion == "Directo")
            .OrderByDescending(ep => ep.FechaAsignacion)
            .Take(porFuente)
            .ToListAsync();
        items.AddRange(perifericosDirectos.Select(ep => new ActividadItem
        {
            Fecha = ep.TipoMovimiento == "Devolucion" ? ep.FechaDesvinculacion ?? ep.FechaAsignacion : ep.FechaAsignacion,
            Icono = ep.TipoMovimiento == "Devolucion" ? "bi-arrow-return-left" : "bi-person-check-fill",
            BadgeClase = ep.TipoMovimiento == "Devolucion" ? "badge-green" : "badge-asignado",
            Titulo = ep.TipoMovimiento == "Devolucion" ? "Devolución de periférico" : "Asignación directa de periférico",
            Descripcion = $"{ep.Periferico?.Marca} {ep.Periferico?.Modelo}",
            Responsable = ep.NombreResponsable,
            Usuario = ep.CreadoPorUsuario?.NombreCompleto,
            LinkController = "Perifericos", LinkAction = "Details", LinkId = ep.PerifericoId
        }));

        var licenciasDirectas = await _db.LicenciasAsignaciones
            .Include(la => la.TipoLicencia)
            .Include(la => la.CreadoPorUsuario)
            .Where(la => la.TipoAsignacion == "Directo")
            .OrderByDescending(la => la.FechaAsignacion)
            .Take(porFuente)
            .ToListAsync();
        items.AddRange(licenciasDirectas.Select(la => new ActividadItem
        {
            Fecha = la.TipoMovimiento == "Devolucion" ? la.FechaDesvinculacion ?? la.FechaAsignacion : la.FechaAsignacion,
            Icono = la.TipoMovimiento == "Devolucion" ? "bi-arrow-return-left" : "bi-key-fill",
            BadgeClase = la.TipoMovimiento == "Devolucion" ? "badge-green" : "badge-asignado",
            Titulo = la.TipoMovimiento == "Devolucion" ? "Licencia revocada" : "Licencia asignada",
            Descripcion = la.TipoLicencia?.Nombre ?? "",
            Responsable = la.NombreResponsable,
            Usuario = la.CreadoPorUsuario?.NombreCompleto,
            LinkController = "Licencias", LinkAction = "Details", LinkId = la.TipoLicenciaId
        }));

        var empleados = await _db.Empleados
            .Include(e => e.CreadoPorUsuario)
            .OrderByDescending(e => e.FechaRegistro)
            .Take(porFuente)
            .ToListAsync();
        items.AddRange(empleados.Select(e => new ActividadItem
        {
            Fecha = e.FechaRegistro, Icono = "bi-person-fill", BadgeClase = "badge-bodega", Titulo = "Empleado registrado",
            Descripcion = e.Nombre, Usuario = e.CreadoPorUsuario?.NombreCompleto,
            LinkController = "Empleados", LinkAction = "Details", LinkId = e.Id
        }));

        var miembros = await _db.MiembrosExternos
            .Include(m => m.CreadoPorUsuario)
            .OrderByDescending(m => m.FechaRegistro)
            .Take(porFuente)
            .ToListAsync();
        items.AddRange(miembros.Select(m => new ActividadItem
        {
            Fecha = m.FechaRegistro, Icono = "bi-person-badge", BadgeClase = "badge-bodega", Titulo = "Miembro externo registrado",
            Descripcion = m.Nombre, Usuario = m.CreadoPorUsuario?.NombreCompleto,
            LinkController = "MiembrosExternos", LinkAction = "Details", LinkId = m.Id
        }));

        var grupos = await _db.Grupos
            .Include(g => g.CreadoPorUsuario)
            .OrderByDescending(g => g.FechaRegistro)
            .Take(porFuente)
            .ToListAsync();
        items.AddRange(grupos.Select(g => new ActividadItem
        {
            Fecha = g.FechaRegistro, Icono = "bi-diagram-3", BadgeClase = "badge-bodega", Titulo = "Grupo registrado",
            Descripcion = g.Nombre, Usuario = g.CreadoPorUsuario?.NombreCompleto,
            LinkController = "Grupos", LinkAction = "Details", LinkId = g.Id
        }));

        return items.OrderByDescending(i => i.Fecha).Take(totalFeed).ToList();
    }
}
