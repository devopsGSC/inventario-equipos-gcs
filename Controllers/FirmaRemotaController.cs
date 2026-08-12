using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Filters;
using InventarioTI.Models;

namespace InventarioTI.Controllers;

// Página pública (sin login) para que un colaborador que no está
// físicamente presente firme un movimiento desde un link de un solo uso.
[AllowAnonymous, NoTrackNavigation]
public class FirmaRemotaController : Controller
{
    private readonly AppDbContext _db;
    public FirmaRemotaController(AppDbContext db) => _db = db;

    private Task<SolicitudFirma?> BuscarSolicitud(string token) =>
        _db.SolicitudesFirma
            .Include(s => s.Movimiento).ThenInclude(m => m!.Equipo)
            .Include(s => s.Movimiento).ThenInclude(m => m!.Empleado)
            .Include(s => s.Movimiento).ThenInclude(m => m!.MiembroExterno)
            .Include(s => s.Movimiento).ThenInclude(m => m!.Grupo)
            .Include(s => s.EquipoPeriferico).ThenInclude(ep => ep!.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(s => s.EquipoPeriferico).ThenInclude(ep => ep!.Empleado)
            .Include(s => s.EquipoPeriferico).ThenInclude(ep => ep!.MiembroExterno)
            .Include(s => s.EquipoPeriferico).ThenInclude(ep => ep!.Grupo)
            .Include(s => s.CartaGeneral).ThenInclude(c => c!.Empleado)
            .Include(s => s.CartaGeneral).ThenInclude(c => c!.MiembroExterno)
            .Include(s => s.CartaGeneral).ThenInclude(c => c!.Grupo)
            .FirstOrDefaultAsync(s => s.Token == token);

    // Periféricos y licencias de la misma persona/miembro externo/grupo del
    // movimiento, para mostrarlos junto con el equipo en la pantalla de
    // firma ("también estás recibiendo").
    private async Task CargarLicencias(Movimiento movimiento)
    {
        ViewBag.Perifericos = await _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Where(ep => ep.FechaDesvinculacion == null &&
                ((movimiento.EmpleadoId != null && ep.EmpleadoId == movimiento.EmpleadoId) ||
                 (movimiento.MiembroExternoId != null && ep.MiembroExternoId == movimiento.MiembroExternoId) ||
                 (movimiento.GrupoId != null && ep.GrupoId == movimiento.GrupoId)))
            .ToListAsync();

        ViewBag.Licencias = await _db.LicenciasAsignaciones
            .Include(la => la.TipoLicencia)
            .Where(la => la.FechaDesvinculacion == null &&
                ((movimiento.EmpleadoId != null && la.EmpleadoId == movimiento.EmpleadoId) ||
                 (movimiento.MiembroExternoId != null && la.MiembroExternoId == movimiento.MiembroExternoId) ||
                 (movimiento.GrupoId != null && la.GrupoId == movimiento.GrupoId)))
            .ToListAsync();
    }

    // Para la carta general se listan los equipos y perifericos actuales
    // de la persona/grupo, igual que en la pantalla de estado de la carta.
    private async Task CargarActivosCartaGeneral(CartaGeneral carta)
    {
        var equiposQ = _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => m.FechaDevolucion == null && (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"));
        var perifsQ = _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(ep => ep.Equipo)
            .Where(ep => ep.FechaDesvinculacion == null);

        if (carta.EmpleadoId != null)
        {
            equiposQ = equiposQ.Where(m => m.EmpleadoId == carta.EmpleadoId);
            perifsQ  = perifsQ.Where(ep => ep.EmpleadoId == carta.EmpleadoId);
        }
        else if (carta.MiembroExternoId != null)
        {
            equiposQ = equiposQ.Where(m => m.MiembroExternoId == carta.MiembroExternoId);
            perifsQ  = perifsQ.Where(ep => ep.MiembroExternoId == carta.MiembroExternoId);
        }
        else if (carta.GrupoId != null)
        {
            equiposQ = equiposQ.Where(m => m.GrupoId == carta.GrupoId);
            perifsQ  = perifsQ.Where(ep => ep.GrupoId == carta.GrupoId);
        }

        ViewBag.EquiposCartaGeneral     = await equiposQ.ToListAsync();
        ViewBag.PerifericosCartaGeneral = await perifsQ.ToListAsync();
    }

    // true si la solicitud es válida (existe, sin usar, sin expirar y con
    // exactamente una entidad asociada); si no, devuelve la vista de error.
    private IActionResult? Validar(SolicitudFirma? solicitud)
    {
        if (solicitud == null) return View("Invalido", "Este link no es válido.");
        if (solicitud.FechaFirmado.HasValue) return View("Invalido", "Esta firma ya fue registrada anteriormente.");
        if (solicitud.FechaExpiracion < DateTime.Now) return View("Invalido", "Este link expiró. Solicitá uno nuevo.");
        if (solicitud.Movimiento == null && solicitud.EquipoPeriferico == null && solicitud.CartaGeneral == null) return View("Invalido", "Este link no es válido.");
        return null;
    }

    [HttpGet("Firmar/{token}")]
    public async Task<IActionResult> Firmar(string token)
    {
        var solicitud = await BuscarSolicitud(token);
        var error = Validar(solicitud);
        if (error != null) return error;

        if (solicitud!.Movimiento != null) await CargarLicencias(solicitud.Movimiento);
        else if (solicitud.CartaGeneral != null) await CargarActivosCartaGeneral(solicitud.CartaGeneral);
        return View(solicitud);
    }

    [HttpPost("Firmar/{token}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Firmar(string token, string? firmaEmpleado)
    {
        var solicitud = await BuscarSolicitud(token);
        var error = Validar(solicitud);
        if (error != null) return error;

        if (string.IsNullOrWhiteSpace(firmaEmpleado))
        {
            ViewBag.Error = "Tenés que firmar antes de continuar.";
            if (solicitud!.Movimiento != null) await CargarLicencias(solicitud.Movimiento);
            else if (solicitud.CartaGeneral != null) await CargarActivosCartaGeneral(solicitud.CartaGeneral);
            return View(solicitud);
        }

        if (solicitud!.Movimiento != null) solicitud.Movimiento.FirmaEmpleado = firmaEmpleado;
        else if (solicitud.EquipoPeriferico != null) solicitud.EquipoPeriferico.FirmaEmpleado = firmaEmpleado;
        else solicitud.CartaGeneral!.FirmaEmpleado = firmaEmpleado;
        solicitud.FechaFirmado = DateTime.Now;
        await _db.SaveChangesAsync();

        return View("Confirmacion", solicitud);
    }
}
