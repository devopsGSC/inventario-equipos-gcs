using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;

namespace InventarioTI.Controllers;

// Carta de autorizacion para que un empleado interponga aviso o denuncia
// ante las autoridades (PNC / FGR) por perdida, hurto o robo de uno o
// varios de sus equipos asignados. A diferencia de las cartas de
// asignacion/carta general, aqui firma "quien autoriza" (un usuario del
// sistema con firma cargada), nunca el empleado.
public class CartasDenunciaController : BaseController
{
    private readonly AppDbContext _db;
    private readonly PdfService _pdf;
    private readonly PdfSigningService _pdfFirma;
    private readonly UserManager<UsuarioApp> _users;
    public CartasDenunciaController(AppDbContext db, PdfService pdf, PdfSigningService pdfFirma, UserManager<UsuarioApp> users, PermisoService permisos) : base(permisos)
    { _db = db; _pdf = pdf; _pdfFirma = pdfFirma; _users = users; }

    public async Task<IActionResult> Nueva(int empleadoId)
    {
        if (!await Puede("empleados.cartadenuncia")) return AccesoDenegado();

        var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (empleado == null) return NotFound();

        var equipos = await _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => m.EmpleadoId == empleadoId && m.FechaDevolucion == null &&
                        (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"))
            .Select(m => m.Equipo!)
            .ToListAsync();

        var autorizantes = await _users.Users
            .Where(u => u.Activo && u.RutaFirmaIT != null)
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync();

        ViewBag.Empleado    = empleado;
        ViewBag.Equipos     = equipos;
        ViewBag.Autorizantes = autorizantes;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generar(int empleadoId, List<int> equipoIds, string usuarioAutorizaId)
    {
        if (!await Puede("empleados.cartadenuncia")) return AccesoDenegado();

        var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (empleado == null) return NotFound();

        if (equipoIds == null || equipoIds.Count == 0)
        {
            TempData["Error"] = "Selecciona al menos un equipo.";
            return RedirectToAction(nameof(Nueva), new { empleadoId });
        }

        var autorizante = await _users.FindByIdAsync(usuarioAutorizaId);
        if (autorizante == null || string.IsNullOrEmpty(autorizante.RutaFirmaIT))
        {
            TempData["Error"] = "Selecciona un usuario válido que tenga firma cargada.";
            return RedirectToAction(nameof(Nueva), new { empleadoId });
        }
        if (string.IsNullOrWhiteSpace(autorizante.DUI))
        {
            TempData["Error"] = $"{autorizante.NombreCompleto} no tiene DUI registrado. Complétalo en Usuarios antes de generar esta carta.";
            return RedirectToAction(nameof(Nueva), new { empleadoId });
        }

        // Los equipos deben estar realmente asignados a este empleado — no
        // se confia en los ids que llegan del formulario sin verificar.
        var equiposValidos = await _db.Movimientos
            .Where(m => m.EmpleadoId == empleadoId && m.FechaDevolucion == null &&
                        (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo") &&
                        equipoIds.Contains(m.EquipoId))
            .Select(m => m.EquipoId)
            .Distinct()
            .ToListAsync();
        if (equiposValidos.Count == 0)
        {
            TempData["Error"] = "Los equipos seleccionados no están asignados a este empleado.";
            return RedirectToAction(nameof(Nueva), new { empleadoId });
        }

        var registro = new CartaAutorizacionDenuncia
        {
            EmpleadoId         = empleadoId,
            EquiposIds         = string.Join(",", equiposValidos),
            UsuarioAutorizaId  = autorizante.Id,
            CreadoPorUsuarioId = UsuarioActualId,
            FechaCreacion      = DateTime.Now
        };
        _db.CartasAutorizacionDenuncia.Add(registro);
        await _db.SaveChangesAsync();

        var bytes = _pdfFirma.Firmar(await GenerarPdf(empleado, equiposValidos, autorizante, registro.FechaCreacion));
        var nombre = $"Carta_Autorizacion_Denuncia_{SanitizarNombreArchivo(empleado.Nombre)}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    // Vuelve a generar el PDF de un registro ya existente, con los datos
    // actuales de empleado/equipos/autorizante (no se guarda una copia del
    // PDF — igual que la Carta General, se reconstruye a partir del estado
    // actual del sistema).
    public async Task<IActionResult> Redescargar(int id)
    {
        if (!await Puede("empleados.cartadenuncia")) return AccesoDenegado();

        var registro = await _db.CartasAutorizacionDenuncia
            .Include(c => c.Empleado)
            .Include(c => c.UsuarioAutoriza)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (registro == null || registro.Empleado == null || registro.UsuarioAutoriza == null) return NotFound();

        var bytes = _pdfFirma.Firmar(await GenerarPdf(registro.Empleado, registro.EquipoIdsList.ToList(), registro.UsuarioAutoriza, registro.FechaCreacion));
        var nombre = $"Carta_Autorizacion_Denuncia_{SanitizarNombreArchivo(registro.Empleado.Nombre)}_{registro.FechaCreacion:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    private async Task<byte[]> GenerarPdf(Empleado empleado, List<int> equipoIds, UsuarioApp autorizante, DateTime fecha)
    {
        var equipos = await _db.Equipos
            .Where(e => equipoIds.Contains(e.Id))
            .Select(e => new EquipoDenunciaItem
            {
                Marca            = e.Marca,
                Modelo           = e.Modelo,
                NumeroSerie      = e.NumeroSerie,
                Imei             = e.IMEI,
                NumeroCelular    = e.NumeroCelular,
                CodigoActivoFijo = e.CodigoActivoFijo
            })
            .ToListAsync();

        var data = new CartaAutorizacionDenunciaData
        {
            Fecha          = fecha,
            NombreAutoriza = autorizante.NombreCompleto,
            DuiAutoriza    = autorizante.DUI ?? "",
            CargoAutoriza  = autorizante.Cargo,
            RutaFirmaIT    = autorizante.RutaFirmaIT,
            NombreEmpleado = empleado.Nombre,
            DuiEmpleado    = empleado.DUI,
            Equipos        = equipos
        };
        return _pdf.GenerarCartaAutorizacionDenuncia(data);
    }

    private static string SanitizarNombreArchivo(string nombre) =>
        string.Join("_", nombre.Split(Path.GetInvalidFileNameChars().Append(' ').ToArray(), StringSplitOptions.RemoveEmptyEntries));
}
