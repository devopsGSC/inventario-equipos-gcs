using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Services;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

// Pantalla central de cartas: junta en una sola lista las de equipo
// (Movimientos), las de periféricos asignados "Directo" (Perifericos) y
// las cartas generales (CartasGenerales), con su estado de entrega y
// acciones — para no tener que ir a buscarlas por separado en el
// detalle de cada equipo/empleado/periférico.
public class CartasController : BaseController
{
    private readonly AppDbContext _db;
    public CartasController(AppDbContext db, PermisoService permisos) : base(permisos) => _db = db;

    public async Task<IActionResult> Index(string? estado, string? tipo, string? buscar, int pagina = 1)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        const int tamPagina = 20;
        var todas = await ObtenerCartas();

        if (estado == "Pendiente") todas = todas.Where(c => !c.EntregaCompletada).ToList();
        else if (estado == "Entregada") todas = todas.Where(c => c.EntregaCompletada).ToList();

        if (!string.IsNullOrEmpty(tipo))
            todas = todas.Where(c => c.Tipo == tipo).ToList();

        if (!string.IsNullOrEmpty(buscar))
            todas = todas.Where(c => c.Responsable.Contains(buscar, StringComparison.OrdinalIgnoreCase)).ToList();

        var ordenadas = todas.OrderByDescending(c => c.Fecha).ToList();
        var total = ordenadas.Count;
        var pageItems = ordenadas.Skip((pagina - 1) * tamPagina).Take(tamPagina).ToList();

        ViewBag.Estado = estado;
        ViewBag.Tipo = tipo;
        ViewBag.Buscar = buscar;
        ViewBag.Paginacion = new PaginacionViewModel
        {
            PaginaActual   = pagina,
            TotalPaginas   = (int)Math.Ceiling(total / (double)tamPagina),
            TotalRegistros = total,
            TamañoPagina   = tamPagina
        };
        return View(pageItems);
    }

    private async Task<List<CartaListItem>> ObtenerCartas()
    {
        var movimientos = await _db.Movimientos
            .Include(m => m.Empleado)
            .Include(m => m.MiembroExterno)
            .Include(m => m.Grupo)
            .Include(m => m.Equipo)
            .Include(m => m.EntregadoPorUsuario)
            .Where(m => (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo") &&
                        (m.EmpleadoId != null || m.MiembroExternoId != null || m.GrupoId != null))
            .ToListAsync();

        var perifsDirectos = await _db.EquiposPerifericos
            .Include(ep => ep.Empleado)
            .Include(ep => ep.MiembroExterno)
            .Include(ep => ep.Grupo)
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.EntregadoPorUsuario)
            .Where(ep => ep.EmpleadoId != null || ep.MiembroExternoId != null || ep.GrupoId != null)
            .ToListAsync();

        var generales = await _db.CartasGenerales
            .Include(c => c.Empleado)
            .Include(c => c.MiembroExterno)
            .Include(c => c.Grupo)
            .Include(c => c.EntregadoPorUsuario)
            .ToListAsync();

        var lista = new List<CartaListItem>();

        lista.AddRange(movimientos.Select(m => new CartaListItem
        {
            Tipo               = "Equipo",
            Responsable        = m.NombreResponsable,
            Detalle            = $"{m.TipoMovimiento} — {m.Equipo?.NombreEquipo}",
            Fecha              = m.FechaInicio,
            EntregaCompletada  = m.EntregaCompletada,
            EntregadoPor       = m.EntregadoPorUsuario?.NombreCompleto,
            FechaEntrega       = m.FechaEntrega,
            TieneFirma         = !string.IsNullOrEmpty(m.FirmaEmpleado),
            LinkVer            = Url.Action("Carta", "Movimientos", new { id = m.Id })!,
            LinkDescargar      = Url.Action("DescargarCarta", "Movimientos", new { id = m.Id })!,
            LinkFirmaRemota    = string.IsNullOrEmpty(m.FirmaEmpleado) ? Url.Action("GenerarLinkFirma", "Movimientos", new { id = m.Id }) : null
        }));

        lista.AddRange(perifsDirectos.Select(ep => new CartaListItem
        {
            Tipo               = "Periferico",
            Responsable        = ep.NombreResponsable,
            Detalle            = $"{ep.Periferico?.TipoPeriferico?.Nombre} — {ep.Periferico?.Marca} {ep.Periferico?.Modelo}",
            Fecha              = ep.FechaAsignacion,
            EntregaCompletada  = ep.EntregaCompletada,
            EntregadoPor       = ep.EntregadoPorUsuario?.NombreCompleto,
            FechaEntrega       = ep.FechaEntrega,
            TieneFirma         = !string.IsNullOrEmpty(ep.FirmaEmpleado),
            LinkVer            = Url.Action("CartaDirecta", "Perifericos", new { asignacionId = ep.Id })!,
            LinkDescargar      = Url.Action("DescargarCartaDirecta", "Perifericos", new { asignacionId = ep.Id })!,
            LinkFirmaRemota    = string.IsNullOrEmpty(ep.FirmaEmpleado) ? Url.Action("GenerarLinkFirma", "Perifericos", new { asignacionId = ep.Id }) : null
        }));

        lista.AddRange(generales.Select(c => new CartaListItem
        {
            Tipo               = "General",
            Responsable        = c.NombreResponsable,
            Detalle            = "Carta general de activos",
            Fecha              = c.FechaCreacion,
            EntregaCompletada  = c.EntregaCompletada,
            EntregadoPor       = c.EntregadoPorUsuario?.NombreCompleto,
            FechaEntrega       = c.FechaEntrega,
            TieneFirma         = !string.IsNullOrEmpty(c.FirmaEmpleado),
            LinkVer            = Url.Action("Carta", "CartasGenerales", new { id = c.Id })!,
            LinkDescargar      = Url.Action("DescargarCarta", "CartasGenerales", new { id = c.Id })!,
            LinkFirmaRemota    = string.IsNullOrEmpty(c.FirmaEmpleado) ? Url.Action("GenerarLinkFirma", "CartasGenerales", new { id = c.Id }) : null
        }));

        return lista;
    }
}
