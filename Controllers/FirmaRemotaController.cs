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
            .Include(s => s.Movimiento).ThenInclude(m => m!.Equipo).ThenInclude(e => e!.EquiposPerifericos.Where(ep => ep.FechaDesvinculacion == null))
                .ThenInclude(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(s => s.Movimiento).ThenInclude(m => m!.Empleado)
            .Include(s => s.Movimiento).ThenInclude(m => m!.MiembroExterno)
            .Include(s => s.Movimiento).ThenInclude(m => m!.Grupo)
            .Include(s => s.EquipoPeriferico).ThenInclude(ep => ep!.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Include(s => s.EquipoPeriferico).ThenInclude(ep => ep!.Empleado)
            .Include(s => s.EquipoPeriferico).ThenInclude(ep => ep!.MiembroExterno)
            .Include(s => s.EquipoPeriferico).ThenInclude(ep => ep!.Grupo)
            .FirstOrDefaultAsync(s => s.Token == token);

    private async Task CargarLicencias(Movimiento movimiento)
    {
        ViewBag.Licencias = await _db.LicenciasAsignaciones
            .Include(la => la.TipoLicencia)
            .Where(la => la.EquipoId == movimiento.EquipoId && la.FechaDesvinculacion == null)
            .ToListAsync();
    }

    // true si la solicitud es válida (existe, sin usar, sin expirar y con
    // exactamente una entidad asociada); si no, devuelve la vista de error.
    private IActionResult? Validar(SolicitudFirma? solicitud)
    {
        if (solicitud == null) return View("Invalido", "Este link no es válido.");
        if (solicitud.FechaFirmado.HasValue) return View("Invalido", "Esta firma ya fue registrada anteriormente.");
        if (solicitud.FechaExpiracion < DateTime.Now) return View("Invalido", "Este link expiró. Solicitá uno nuevo.");
        if (solicitud.Movimiento == null && solicitud.EquipoPeriferico == null) return View("Invalido", "Este link no es válido.");
        return null;
    }

    [HttpGet("Firmar/{token}")]
    public async Task<IActionResult> Firmar(string token)
    {
        var solicitud = await BuscarSolicitud(token);
        var error = Validar(solicitud);
        if (error != null) return error;

        if (solicitud!.Movimiento != null) await CargarLicencias(solicitud.Movimiento);
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
            return View(solicitud);
        }

        if (solicitud!.Movimiento != null) solicitud.Movimiento.FirmaEmpleado = firmaEmpleado;
        else solicitud.EquipoPeriferico!.FirmaEmpleado = firmaEmpleado;
        solicitud.FechaFirmado = DateTime.Now;
        await _db.SaveChangesAsync();

        return View("Confirmacion", solicitud);
    }
}
