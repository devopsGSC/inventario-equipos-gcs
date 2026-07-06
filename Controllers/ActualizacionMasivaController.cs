using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.Text.Json;
using InventarioTI.Data;
using InventarioTI.Models;

namespace InventarioTI.Controllers;

[Authorize(Roles = "Administrador")]
public class ActualizacionMasivaController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<UsuarioApp> _users;
    public ActualizacionMasivaController(AppDbContext db, UserManager<UsuarioApp> users)
    { _db = db; _users = users; }

    public IActionResult Index() => View();

    // Descargar plantilla
    public IActionResult Plantilla()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                                "plantilla_actualizacion_equipos.xlsx");
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Plantilla_Actualizacion_Equipos.xlsx");
    }

    // PASO 1: Previsualizar sin guardar
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Previsualizar(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
        {
            TempData["Error"] = "Selecciona un archivo Excel válido.";
            return RedirectToAction(nameof(Index));
        }

        // Cargar todos los equipos existentes indexados por serie para lookup O(1)
        var equiposExistentes = await _db.Equipos
            .Include(e => e.TipoEquipo)
            .ToDictionaryAsync(e => e.NumeroSerie, e => e);
        var tiposEquipo = await _db.TiposEquipo.ToListAsync();

        var preview = new List<EquipoActualizacionPrevio>();

        using var stream = new MemoryStream();
        await archivo.CopyToAsync(stream);
        stream.Position = 0;

        try
        {
            using var wb = new XLWorkbook(stream);
            var ws   = wb.Worksheet(1);
            int fila = 5;

            while (fila <= 1004)
            {
                var row = ws.Row(fila);
                if (string.IsNullOrWhiteSpace(row.Cell(1).GetString())) break;

                string serie       = row.Cell(1).GetString().Trim();
                string nombre      = row.Cell(2).GetString().Trim();
                string tipo        = row.Cell(3).GetString().Trim();
                string marca       = row.Cell(4).GetString().Trim();
                string modelo      = row.Cell(5).GetString().Trim();
                string imei        = row.Cell(6).GetString().Trim();
                string accesorios  = row.Cell(7).GetString().Trim();
                string costoStr    = row.Cell(8).GetString().Trim();
                string fCompraStr  = row.Cell(9).GetString().Trim();
                string fGarantStr  = row.Cell(10).GetString().Trim();
                string estado      = row.Cell(11).GetString().Trim();
                string obs         = row.Cell(12).GetString().Trim();

                var ep = new EquipoActualizacionPrevio
                {
                    Fila          = fila,
                    NumeroSerie   = serie,
                    NombreEquipo  = nombre,
                    TipoEquipo    = tipo,
                    Marca         = marca,
                    Modelo        = modelo,
                    IMEI          = imei,
                    Accesorios    = accesorios,
                    CostoStr      = costoStr,
                    FechaCompraStr= fCompraStr,
                    FechaGarantStr= fGarantStr,
                    Estado        = estado,
                    Observaciones = obs
                };

                // Validaciones
                if (string.IsNullOrEmpty(serie))
                {
                    ep.EstadoPreview = "Error";
                    ep.MensajePreview = "NumeroSerie es obligatorio para identificar el equipo.";
                    preview.Add(ep); fila++; continue;
                }

                if (!equiposExistentes.ContainsKey(serie))
                {
                    ep.EstadoPreview  = "Error";
                    ep.MensajePreview = $"No existe ningún equipo con la serie '{serie}' en el sistema.";
                    preview.Add(ep); fila++; continue;
                }

                // Validar estado si viene con valor
                var estadosValidos = new[] { "Bodega","Asignado","Prestamo","EnGarantia","Baja" };
                if (!string.IsNullOrEmpty(estado) && !estadosValidos.Contains(estado))
                {
                    ep.EstadoPreview  = "Error";
                    ep.MensajePreview = $"Estado '{estado}' no válido. Use: {string.Join(", ", estadosValidos)}";
                    preview.Add(ep); fila++; continue;
                }

                // Validar que nombre no esté duplicado en otro equipo
                if (!string.IsNullOrEmpty(nombre))
                {
                    var equipoActual = equiposExistentes[serie];
                    var duplicado = equiposExistentes.Values
                        .Any(e => e.NombreEquipo == nombre && e.Id != equipoActual.Id);
                    if (duplicado)
                    {
                        ep.EstadoPreview  = "Advertencia";
                        ep.MensajePreview = $"Ya existe otro equipo con el nombre '{nombre}'. Se actualizará igual.";
                        // No bloquear — solo advertir
                    }
                }

                // Calcular qué campos se van a modificar
                var equipo  = equiposExistentes[serie];
                var cambios = new List<string>();
                if (!string.IsNullOrEmpty(nombre)      && nombre      != equipo.NombreEquipo)        cambios.Add("Nombre");
                if (!string.IsNullOrEmpty(tipo)        && tipo        != equipo.TipoEquipo?.Nombre)  cambios.Add("Tipo");
                if (!string.IsNullOrEmpty(marca)       && marca       != equipo.Marca)               cambios.Add("Marca");
                if (!string.IsNullOrEmpty(modelo)      && modelo      != equipo.Modelo)              cambios.Add("Modelo");
                if (!string.IsNullOrEmpty(imei)        && imei        != equipo.IMEI)                cambios.Add("IMEI");
                if (!string.IsNullOrEmpty(accesorios)  && accesorios  != equipo.Accesorios)          cambios.Add("Accesorios");
                if (!string.IsNullOrEmpty(estado)      && estado      != equipo.Estado)              cambios.Add($"Estado ({equipo.Estado} → {estado})");
                if (!string.IsNullOrEmpty(costoStr))    cambios.Add("Costo");
                if (!string.IsNullOrEmpty(fCompraStr)) cambios.Add("Fecha compra");
                if (!string.IsNullOrEmpty(fGarantStr)) cambios.Add("Fecha garantía");
                if (!string.IsNullOrEmpty(obs))        cambios.Add("Observaciones");

                if (!cambios.Any() && string.IsNullOrEmpty(ep.EstadoPreview))
                {
                    ep.EstadoPreview  = "SinCambios";
                    ep.MensajePreview = "Sin cambios detectados — todos los campos coinciden o están vacíos.";
                }
                else if (string.IsNullOrEmpty(ep.EstadoPreview))
                {
                    ep.EstadoPreview  = "Valido";
                    ep.MensajePreview = $"Se actualizarán: {string.Join(", ", cambios)}";
                }

                ep.NombreActual = equipo.NombreEquipo;
                preview.Add(ep);
                fila++;
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"No se pudo leer el archivo: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }

        TempData["Preview"] = JsonSerializer.Serialize(preview);
        return View("Preview", preview);
    }

    // PASO 2: Confirmar y aplicar actualizaciones
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar()
    {
        var json = TempData["Preview"] as string;
        if (string.IsNullOrEmpty(json))
        {
            TempData["Error"] = "La sesión expiró. Por favor sube el archivo nuevamente.";
            return RedirectToAction(nameof(Index));
        }

        var preview = JsonSerializer.Deserialize<List<EquipoActualizacionPrevio>>(json) ?? [];

        var equiposExistentes = await _db.Equipos
            .Include(e => e.TipoEquipo)
            .ToDictionaryAsync(e => e.NumeroSerie, e => e);
        var tiposEquipo = await _db.TiposEquipo.ToListAsync();

        int actualizados = 0, omitidos = 0, errores = 0;
        var resultados = new List<ResultadoActualizacion>();

        foreach (var p in preview)
        {
            if (p.EstadoPreview == "SinCambios")
            {
                omitidos++;
                resultados.Add(new ResultadoActualizacion
                    { Fila = p.Fila, NumeroSerie = p.NumeroSerie, NombreEquipo = p.NombreActual,
                      Estado = "Omitido", Mensaje = p.MensajePreview });
                continue;
            }

            if (p.EstadoPreview == "Error")
            {
                errores++;
                resultados.Add(new ResultadoActualizacion
                    { Fila = p.Fila, NumeroSerie = p.NumeroSerie, Estado = "Error", Mensaje = p.MensajePreview });
                continue;
            }

            if (!equiposExistentes.TryGetValue(p.NumeroSerie, out var equipo))
            {
                errores++;
                resultados.Add(new ResultadoActualizacion
                    { Fila = p.Fila, NumeroSerie = p.NumeroSerie, Estado = "Error", Mensaje = "Equipo no encontrado." });
                continue;
            }

            try
            {
                // Actualizar SOLO los campos que vienen con valor (campos vacíos se ignoran)
                if (!string.IsNullOrEmpty(p.NombreEquipo))  equipo.NombreEquipo  = p.NombreEquipo;
                if (!string.IsNullOrEmpty(p.Marca))         equipo.Marca         = p.Marca;
                if (!string.IsNullOrEmpty(p.Modelo))        equipo.Modelo        = p.Modelo;
                if (!string.IsNullOrEmpty(p.IMEI))          equipo.IMEI          = p.IMEI;
                if (!string.IsNullOrEmpty(p.Accesorios))    equipo.Accesorios    = p.Accesorios;
                if (!string.IsNullOrEmpty(p.Estado))        equipo.Estado        = p.Estado;

                if (!string.IsNullOrEmpty(p.TipoEquipo))
                {
                    var tipo = tiposEquipo.FirstOrDefault(t =>
                        t.Nombre.Equals(p.TipoEquipo, StringComparison.OrdinalIgnoreCase));
                    if (tipo == null)
                    {
                        tipo = new TipoEquipo { Nombre = p.TipoEquipo };
                        _db.TiposEquipo.Add(tipo);
                        await _db.SaveChangesAsync();
                        tiposEquipo.Add(tipo);
                    }
                    equipo.TipoEquipoId = tipo.Id;
                }

                if (!string.IsNullOrEmpty(p.CostoStr) &&
                    decimal.TryParse(p.CostoStr.Replace(",","").Replace("$",""),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var costo))
                    equipo.Costo = costo;

                if (!string.IsNullOrEmpty(p.FechaCompraStr))
                {
                    var fc = TryParseDate(p.FechaCompraStr);
                    if (fc.HasValue) equipo.FechaCompra = fc;
                }

                if (!string.IsNullOrEmpty(p.FechaGarantStr))
                {
                    var fg = TryParseDate(p.FechaGarantStr);
                    if (fg.HasValue) equipo.FechaGarantia = fg;
                }

                resultados.Add(new ResultadoActualizacion
                    { Fila = p.Fila, NumeroSerie = p.NumeroSerie, NombreEquipo = equipo.NombreEquipo,
                      Estado = "OK", Mensaje = p.MensajePreview });
                actualizados++;
            }
            catch (Exception ex)
            {
                resultados.Add(new ResultadoActualizacion
                    { Fila = p.Fila, NumeroSerie = p.NumeroSerie, Estado = "Error", Mensaje = ex.Message });
                errores++;
            }
        }

        if (actualizados > 0) await _db.SaveChangesAsync();

        // Registrar en historial de auditoría
        var usuarioActual = await _users.GetUserAsync(User);
        var operacion = new OperacionMasiva
        {
            TipoOperacion   = "ActualizacionMasiva",
            FechaOperacion  = DateTime.Now,
            UsuarioNombre   = usuarioActual?.NombreCompleto ?? User.Identity?.Name ?? "Sistema",
            NombreArchivo   = "actualizacion.xlsx",
            TotalProcesados = actualizados + omitidos + errores,
            TotalExitosos   = actualizados,
            TotalOmitidos   = omitidos,
            TotalErrores    = errores,
            Detalles = resultados.Select(r => new DetalleOperacionMasiva
            {
                FilaExcel         = r.Fila,
                NumeroSerie       = r.NumeroSerie,
                NombreEquipo      = r.NombreEquipo,
                Estado            = r.Estado,
                Mensaje           = r.Mensaje,
                CamposModificados = r.Estado == "OK" ? r.Mensaje : null
            }).ToList()
        };
        _db.OperacionesMasivas.Add(operacion);
        await _db.SaveChangesAsync();
        ViewBag.OperacionId = operacion.Id;

        ViewBag.Actualizados = actualizados;
        ViewBag.Omitidos     = omitidos;
        ViewBag.Errores      = errores;
        ViewBag.Resultados   = resultados;
        return View("Resultado");
    }

    private static DateTime? TryParseDate(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        string[] fmts = ["dd/MM/yyyy","d/M/yyyy","yyyy-MM-dd","MM/dd/yyyy"];
        if (DateTime.TryParseExact(val, fmts,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt)) return dt;
        if (DateTime.TryParse(val, out var dt2)) return dt2;
        return null;
    }
}

// DTOs del módulo
public class EquipoActualizacionPrevio
{
    public int    Fila           { get; set; }
    public string NumeroSerie    { get; set; } = "";
    public string NombreActual   { get; set; } = "";
    public string NombreEquipo   { get; set; } = "";
    public string TipoEquipo     { get; set; } = "";
    public string Marca          { get; set; } = "";
    public string Modelo         { get; set; } = "";
    public string IMEI           { get; set; } = "";
    public string Accesorios     { get; set; } = "";
    public string CostoStr       { get; set; } = "";
    public string FechaCompraStr { get; set; } = "";
    public string FechaGarantStr { get; set; } = "";
    public string Estado         { get; set; } = "";
    public string Observaciones  { get; set; } = "";
    public string EstadoPreview  { get; set; } = "";  // Valido | Advertencia | SinCambios | Error
    public string MensajePreview { get; set; } = "";
}

public class ResultadoActualizacion
{
    public int    Fila         { get; set; }
    public string NumeroSerie  { get; set; } = "";
    public string NombreEquipo { get; set; } = "";
    public string Estado       { get; set; } = "";
    public string Mensaje      { get; set; } = "";
}
