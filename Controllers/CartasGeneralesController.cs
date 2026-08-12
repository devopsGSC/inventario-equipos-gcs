using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;

namespace InventarioTI.Controllers;

// Carta que resume TODOS los equipos y perifericos actuales de un
// empleado, miembro externo o grupo en un solo documento firmable,
// con el mismo ciclo de preparacion/entrega y firma remota que ya
// existe para un movimiento de equipo individual.
public class CartasGeneralesController : BaseController
{
    private readonly AppDbContext _db;
    private readonly PdfService _pdf;
    private readonly UserManager<UsuarioApp> _users;
    public CartasGeneralesController(AppDbContext db, PdfService pdf, UserManager<UsuarioApp> users, PermisoService permisos) : base(permisos)
    { _db = db; _pdf = pdf; _users = users; }

    // Crea (o reutiliza si sigue pendiente de entrega) la carta general de
    // este empleado/miembro externo/grupo y va a su pantalla de estado.
    public async Task<IActionResult> Preparar(string tipo, int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        // Siempre se reutiliza la carta general más reciente de esta persona,
        // sin importar su estado: así, si ya se entregó, el botón te lleva a
        // ver quién la entregó y cuándo en vez de crear una en blanco encima.
        // El PDF que genera siempre refleja los equipos/periféricos actuales,
        // así que no hace falta una carta nueva por cada cambio de inventario.
        var carta = tipo switch
        {
            "empleado"       => await _db.CartasGenerales.Where(c => c.EmpleadoId == id).OrderByDescending(c => c.FechaCreacion).FirstOrDefaultAsync(),
            "miembroExterno" => await _db.CartasGenerales.Where(c => c.MiembroExternoId == id).OrderByDescending(c => c.FechaCreacion).FirstOrDefaultAsync(),
            "grupo"          => await _db.CartasGenerales.Where(c => c.GrupoId == id).OrderByDescending(c => c.FechaCreacion).FirstOrDefaultAsync(),
            _ => null
        };

        if (carta == null)
        {
            bool existe = tipo switch
            {
                "empleado"       => await _db.Empleados.AnyAsync(e => e.Id == id),
                "miembroExterno" => await _db.MiembrosExternos.AnyAsync(m => m.Id == id),
                "grupo"          => await _db.Grupos.AnyAsync(g => g.Id == id),
                _ => false
            };
            if (!existe) return NotFound();

            carta = new CartaGeneral
            {
                EmpleadoId         = tipo == "empleado" ? id : null,
                MiembroExternoId   = tipo == "miembroExterno" ? id : null,
                GrupoId            = tipo == "grupo" ? id : null,
                CreadoPorUsuarioId = UsuarioActualId
            };
            _db.CartasGenerales.Add(carta);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Carta), new { id = carta.Id });
    }

    public async Task<IActionResult> Carta(int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var carta = await _db.CartasGenerales
            .Include(c => c.Empleado).ThenInclude(e => e!.Departamento)
            .Include(c => c.MiembroExterno)
            .Include(c => c.Grupo)
            .Include(c => c.CreadoPorUsuario)
            .Include(c => c.EntregadoPorUsuario)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (carta == null) return NotFound();

        var (equipos, perifericos) = await ObtenerActivos(carta);
        ViewBag.TotalEquipos     = equipos.Count;
        ViewBag.TotalPerifericos = perifericos.Count;
        return View(carta);
    }

    // Marca la entrega física como completada (ver comentario equivalente
    // en MovimientosController.MarcarEntrega) y vuelve a la pantalla de la
    // carta, ya recargada con el estado "Entregada".
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarEntrega(int id, string? notaEntrega)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var carta = await _db.CartasGenerales.FirstOrDefaultAsync(c => c.Id == id);
        if (carta == null) return NotFound();

        var usuarioActual = await _users.GetUserAsync(User);
        if (!carta.EntregaCompletada && usuarioActual != null)
        {
            carta.EntregadoPorUsuarioId = usuarioActual.Id;
            carta.EntregaCompletada     = true;
            carta.FechaEntrega          = DateTime.Now;
            var nota = string.IsNullOrWhiteSpace(notaEntrega) ? "Entrega registrada." : notaEntrega.Trim();
            carta.Observaciones = AgregarNotaObservacion(carta.Observaciones,
                usuarioActual.NombreCompleto ?? usuarioActual.Email ?? "Usuario", nota);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Carta), new { id });
    }

    // Reclama la atribución de "quién preparó esta carta" para el usuario
    // actual — necesario porque Preparar reutiliza la misma fila
    // indefinidamente, así que quien la haya creado primero (aunque solo
    // haya entrado a revisarla) se queda fijo como emisor por defecto en el
    // PDF hasta que alguien la marque como entregada. Como MarcarEntrega, es
    // una acción explícita: nunca cambia solo por ver o descargar la carta.
    // Una vez entregada queda fija para siempre (igual que EntregadoPorUsuario).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TomarComoPreparador(int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var carta = await _db.CartasGenerales.FirstOrDefaultAsync(c => c.Id == id);
        if (carta == null) return NotFound();

        if (!carta.EntregaCompletada)
        {
            carta.CreadoPorUsuarioId = UsuarioActualId;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Carta), new { id });
    }

    public async Task<IActionResult> DescargarCarta(int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var carta = await _db.CartasGenerales
            .Include(c => c.Empleado).ThenInclude(e => e!.Departamento)
            .Include(c => c.MiembroExterno)
            .Include(c => c.Grupo)
            .Include(c => c.CreadoPorUsuario)
            .Include(c => c.EntregadoPorUsuario)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (carta == null) return NotFound();

        var usuarioActual = await _users.GetUserAsync(User);

        // Quién entrega se fija explícitamente con MarcarEntrega (el usuario
        // la marca a mano); descargar la carta para revisarla no implica
        // que ya se entregó.
        var (colaborador, centro, area, codEmpleado, identificacion) = DatosResponsable(carta);
        var (equipos, perifericos) = await ObtenerActivos(carta);
        var usuarioEmisor = carta.EntregadoPorUsuario ?? carta.CreadoPorUsuario ?? usuarioActual;

        var data = new CartaGeneralData
        {
            Colaborador         = colaborador,
            Centro              = centro,
            Area                = area,
            CodEmpleado         = codEmpleado,
            Identificacion      = identificacion,
            NombreEmisor        = usuarioEmisor?.NombreCompleto,
            RutaFirmaIT         = usuarioEmisor?.RutaFirmaIT,
            FirmaEmpleadoBase64 = carta.FirmaEmpleado,
            Equipos             = equipos,
            Perifericos         = perifericos
        };

        var bytes = _pdf.GenerarCartaGeneralCompleta(data);
        carta.CartaGenerada = true;
        await _db.SaveChangesAsync();

        var nombre = $"Carta_General_{SanitizarNombreArchivo(colaborador)}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    // Genera (o reutiliza si sigue vigente) un link de un solo uso para que
    // la persona firme la carta general a distancia cuando no está
    // físicamente presente para firmar en el acto.
    public async Task<IActionResult> GenerarLinkFirma(int id)
    {
        if (!await Puede("movimientos.carta")) return AccesoDenegado();

        var carta = await _db.CartasGenerales.FirstOrDefaultAsync(c => c.Id == id);
        if (carta == null) return NotFound();

        if (!string.IsNullOrEmpty(carta.FirmaEmpleado))
        {
            TempData["Error"] = "Esta carta ya tiene una firma registrada.";
            return RedirectToAction(nameof(Carta), new { id });
        }

        var solicitud = await _db.SolicitudesFirma
            .Where(s => s.CartaGeneralId == id && s.FechaFirmado == null && s.FechaExpiracion >= DateTime.Now)
            .OrderByDescending(s => s.FechaCreacion)
            .FirstOrDefaultAsync();

        if (solicitud == null)
        {
            solicitud = new SolicitudFirma
            {
                CartaGeneralId  = id,
                Token           = TokenGenerator.GenerarTokenUrlSafe(),
                FechaCreacion   = DateTime.Now,
                FechaExpiracion = DateTime.Now.AddDays(3)
            };
            _db.SolicitudesFirma.Add(solicitud);
            await _db.SaveChangesAsync();
        }

        ViewBag.Link  = Url.Action("Firmar", "FirmaRemota", new { token = solicitud.Token }, Request.Scheme);
        ViewBag.Carta = carta;
        return View(solicitud);
    }

    private static (string Colaborador, string Centro, string Area, string CodEmpleado, string Identificacion) DatosResponsable(CartaGeneral c)
    {
        if (c.Empleado != null)
            return (c.Empleado.Nombre, c.Empleado.Departamento?.Nombre ?? "", c.Empleado.Cargo ?? "", c.Empleado.CodigoEmpleado, c.Empleado.DUI ?? "");
        if (c.MiembroExterno != null)
            return (c.MiembroExterno.Nombre, c.MiembroExterno.Organizacion ?? "", c.MiembroExterno.Referencia ?? "", "N/A", c.MiembroExterno.Identificacion ?? "");
        if (c.Grupo != null)
            return (c.Grupo.Nombre, c.Grupo.Descripcion ?? "", "", "N/A", "");
        return ("", "", "", "N/A", "");
    }

    private async Task<(List<EquipoResumenItem> Equipos, List<PerifericoResumenItem> Perifericos)> ObtenerActivos(CartaGeneral c)
    {
        var equiposQ = _db.Movimientos
            .Include(m => m.Equipo).ThenInclude(eq => eq!.TipoEquipo)
            .Where(m => m.FechaDevolucion == null && (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo"));
        var perifsQ = _db.EquiposPerifericos
            .Include(ep => ep.Periferico).ThenInclude(p => p!.TipoPeriferico)
            .Where(ep => ep.FechaDesvinculacion == null);

        if (c.EmpleadoId != null)
        {
            equiposQ = equiposQ.Where(m => m.EmpleadoId == c.EmpleadoId);
            perifsQ  = perifsQ.Where(ep => ep.EmpleadoId == c.EmpleadoId);
        }
        else if (c.MiembroExternoId != null)
        {
            equiposQ = equiposQ.Where(m => m.MiembroExternoId == c.MiembroExternoId);
            perifsQ  = perifsQ.Where(ep => ep.MiembroExternoId == c.MiembroExternoId);
        }
        else if (c.GrupoId != null)
        {
            equiposQ = equiposQ.Where(m => m.GrupoId == c.GrupoId);
            perifsQ  = perifsQ.Where(ep => ep.GrupoId == c.GrupoId);
        }
        else
        {
            return ([], []);
        }

        var equipos = await equiposQ.ToListAsync();
        var perifericos = await perifsQ.OrderByDescending(ep => ep.FechaAsignacion).ToListAsync();

        return (
            equipos.Select(m => new EquipoResumenItem
            {
                Tipo        = m.Equipo?.TipoEquipo?.Nombre ?? "",
                Marca       = m.Equipo?.Marca ?? "",
                Modelo      = m.Equipo?.Modelo ?? "",
                NumeroSerie = m.Equipo?.NumeroSerie ?? "",
                Movimiento  = m.TipoMovimiento,
                Desde       = m.FechaInicio.ToString("dd/MM/yyyy"),
                Ram            = m.Equipo?.RAM,
                Procesador     = m.Equipo?.Procesador,
                Almacenamiento = m.Equipo?.Almacenamiento,
                Accesorios     = m.Equipo?.Accesorios,
                FechaGarantia  = m.Equipo?.FechaGarantia?.ToString("dd/MM/yyyy")
            }).ToList(),
            perifericos.Select(ep => new PerifericoResumenItem
            {
                Tipo        = ep.Periferico?.TipoPeriferico?.Nombre ?? "",
                Marca       = ep.Periferico?.Marca ?? "",
                Modelo      = ep.Periferico?.Modelo ?? "",
                NumeroSerie = ep.Periferico?.NumeroSerie ?? ""
            }).ToList()
        );
    }

    private static string AgregarNotaObservacion(string? actual, string usuario, string nota)
    {
        var linea = $"[{DateTime.Now:dd/MM/yyyy HH:mm}] {usuario}: {nota}";
        return string.IsNullOrWhiteSpace(actual) ? linea : $"{actual}\n{linea}";
    }

    private static string SanitizarNombreArchivo(string nombre) =>
        string.Join("_", nombre.Split(Path.GetInvalidFileNameChars().Append(' ').ToArray(), StringSplitOptions.RemoveEmptyEntries));
}
