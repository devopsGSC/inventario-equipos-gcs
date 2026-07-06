using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.Text.Json;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;

namespace InventarioTI.Controllers;

public class CargaMasivaController : BaseController
{
    private readonly AppDbContext _db;
    private readonly ILogger<CargaMasivaController> _logger;
    private readonly UserManager<UsuarioApp> _users;
    public CargaMasivaController(AppDbContext db, ILogger<CargaMasivaController> logger, PermisoService permisos, UserManager<UsuarioApp> users) : base(permisos)
    {
        _db = db;
        _logger = logger;
        _users = users;
    }

    public async Task<IActionResult> Index() => await Puede("cargamasiva.usar") ? View() : AccesoDenegado();

    public async Task<IActionResult> Plantilla()
    {
        if (!await Puede("cargamasiva.usar")) return AccesoDenegado();

        var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "plantilla_equipos.xlsx");
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Plantilla_Carga_Equipos.xlsx");
    }

    // PASO 1: Procesar y previsualizar sin guardar
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Previsualizar(IFormFile archivo)
    {
        if (!await Puede("cargamasiva.usar")) return AccesoDenegado();

        if (archivo == null || archivo.Length == 0)
        {
            TempData["Error"] = "Selecciona un archivo Excel válido.";
            return RedirectToAction(nameof(Index));
        }

        var nombreArchivo    = archivo.FileName;
        var tiposEquipo      = await _db.TiposEquipo.ToListAsync();
        var seriesExistentes = await _db.Equipos.Select(e => e.NumeroSerie).ToHashSetAsync();
        var nombresExistentes= await _db.Equipos.Select(e => e.NombreEquipo).ToHashSetAsync();
        var preview          = new List<EquipoPrevio>();
        var seriesEnArchivo  = new HashSet<string>();

        using var stream = new MemoryStream();
        await archivo.CopyToAsync(stream);
        stream.Position = 0;

        try
        {
            using var wb = new XLWorkbook(stream);
            var ws  = wb.Worksheet(1);
            int fila = 5;

            while (fila <= 1004)
            {
                var row = ws.Row(fila);
                if (string.IsNullOrWhiteSpace(row.Cell(1).GetString())) break;

                string nombre     = row.Cell(1).GetString().Trim();
                string tipoNombre = row.Cell(2).GetString().Trim();
                string marca      = row.Cell(3).GetString().Trim();
                string modelo     = row.Cell(4).GetString().Trim();
                string serie      = row.Cell(5).GetString().Trim();
                string accesorios = row.Cell(6).GetString().Trim();
                string costoStr   = row.Cell(7).GetString().Trim();
                string fCompraStr = row.Cell(8).GetString().Trim();
                string fGarantStr = row.Cell(9).GetString().Trim();

                var ep = new EquipoPrevio
                {
                    Fila          = fila,
                    NombreEquipo  = nombre,
                    TipoEquipo    = tipoNombre,
                    Marca         = marca,
                    Modelo        = modelo,
                    NumeroSerie   = serie,
                    Accesorios    = accesorios,
                    CostoStr      = costoStr,
                    FechaCompraStr= fCompraStr,
                    FechaGarantStr= fGarantStr
                };

                // Validaciones
                var errs = new List<string>();
                if (string.IsNullOrEmpty(nombre))     errs.Add("Nombre requerido");
                if (string.IsNullOrEmpty(tipoNombre)) errs.Add("Tipo requerido");
                if (string.IsNullOrEmpty(marca))      errs.Add("Marca requerida");
                if (string.IsNullOrEmpty(modelo))     errs.Add("Modelo requerido");
                if (string.IsNullOrEmpty(serie))      errs.Add("Serie requerida");

                if (errs.Any())
                    ep.EstadoPreview = "Error";
                else if (seriesExistentes.Contains(serie) || seriesEnArchivo.Contains(serie))
                    ep.EstadoPreview = "Duplicado";
                else if (nombresExistentes.Contains(nombre))
                    ep.EstadoPreview = "NombreDuplicado";
                else
                    ep.EstadoPreview = "Valido";

                ep.MensajePreview = errs.Any() ? string.Join(", ", errs)
                    : ep.EstadoPreview == "Duplicado" ? $"Serie '{serie}' ya existe"
                    : ep.EstadoPreview == "NombreDuplicado" ? $"Nombre '{nombre}' ya existe"
                    : "Listo para registrar";

                if (!string.IsNullOrEmpty(serie)) seriesEnArchivo.Add(serie);
                preview.Add(ep);
                fila++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer el archivo de carga masiva.");
            TempData["Error"] = "No se pudo leer el archivo. Verifica que sea un Excel válido con el formato de la plantilla.";
            return RedirectToAction(nameof(Index));
        }

        // Guardar preview en TempData para el paso de confirmación
        TempData["Preview"] = JsonSerializer.Serialize(preview);
        TempData["PreviewArchivo"] = nombreArchivo;
        return View("Preview", preview);
    }

    // PASO 2: Confirmar y guardar solo los válidos
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar()
    {
        if (!await Puede("cargamasiva.usar")) return AccesoDenegado();

        var json = TempData["Preview"] as string;
        if (string.IsNullOrEmpty(json))
        {
            TempData["Error"] = "La sesión expiró. Por favor sube el archivo nuevamente.";
            return RedirectToAction(nameof(Index));
        }

        var preview = JsonSerializer.Deserialize<List<EquipoPrevio>>(json) ?? [];
        var validos = preview.Where(p => p.EstadoPreview == "Valido").ToList();

        var tiposEquipo = await _db.TiposEquipo.ToListAsync();
        int registrados = 0, omitidos = 0, errores = 0;
        var resultados  = new List<ResultadoCarga>();

        foreach (var ep in preview)
        {
            if (ep.EstadoPreview != "Valido")
            {
                resultados.Add(new ResultadoCarga
                {
                    Fila         = ep.Fila,
                    NumeroSerie  = ep.NumeroSerie,
                    NombreEquipo = ep.NombreEquipo,
                    Estado       = ep.EstadoPreview == "Error" ? "Error" : "Omitido",
                    Mensaje      = ep.MensajePreview
                });
                if (ep.EstadoPreview == "Error") errores++;
                else omitidos++;
                continue;
            }

            try
            {
                var tipo = tiposEquipo.FirstOrDefault(t =>
                    t.Nombre.Equals(ep.TipoEquipo, StringComparison.OrdinalIgnoreCase));
                if (tipo == null)
                {
                    tipo = new TipoEquipo { Nombre = ep.TipoEquipo };
                    _db.TiposEquipo.Add(tipo);
                    await _db.SaveChangesAsync();
                    tiposEquipo.Add(tipo);
                }

                decimal? costo = null;
                if (!string.IsNullOrEmpty(ep.CostoStr) &&
                    decimal.TryParse(ep.CostoStr.Replace(",", "").Replace("$", ""),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var c))
                    costo = c;

                _db.Equipos.Add(new Equipo
                {
                    NombreEquipo  = ep.NombreEquipo,
                    TipoEquipoId  = tipo.Id,
                    Marca         = ep.Marca,
                    Modelo        = ep.Modelo,
                    NumeroSerie   = ep.NumeroSerie,
                    Accesorios    = string.IsNullOrEmpty(ep.Accesorios) ? null : ep.Accesorios,
                    Costo         = costo,
                    FechaCompra   = TryParseDate(ep.FechaCompraStr),
                    FechaGarantia = TryParseDate(ep.FechaGarantStr),
                    Estado        = "Bodega",
                    FechaRegistro = DateTime.Now
                });

                resultados.Add(new ResultadoCarga
                {
                    Fila = ep.Fila, NumeroSerie = ep.NumeroSerie, NombreEquipo = ep.NombreEquipo,
                    Estado = "OK", Mensaje = "Registrado correctamente"
                });
                registrados++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar equipo desde carga masiva (fila {Fila}).", ep.Fila);
                resultados.Add(new ResultadoCarga
                {
                    Fila = ep.Fila, NumeroSerie = ep.NumeroSerie, NombreEquipo = ep.NombreEquipo,
                    Estado = "Error", Mensaje = "Ocurrió un error al registrar este equipo."
                });
                errores++;
            }
        }

        if (registrados > 0) await _db.SaveChangesAsync();

        // Registrar en historial de auditoría
        var nombreArchivo  = TempData["PreviewArchivo"] as string ?? "archivo.xlsx";
        var usuarioActual  = await _users.GetUserAsync(User);
        var operacion = new OperacionMasiva
        {
            TipoOperacion   = "CargaMasiva",
            FechaOperacion  = DateTime.Now,
            UsuarioNombre   = usuarioActual?.NombreCompleto ?? User.Identity?.Name ?? "Sistema",
            NombreArchivo   = nombreArchivo,
            TotalProcesados = registrados + omitidos + errores,
            TotalExitosos   = registrados,
            TotalOmitidos   = omitidos,
            TotalErrores    = errores,
            Detalles = resultados.Select(r => new DetalleOperacionMasiva
            {
                FilaExcel    = r.Fila,
                NumeroSerie  = r.NumeroSerie,
                NombreEquipo = r.NombreEquipo,
                Estado       = r.Estado,
                Mensaje      = r.Mensaje
            }).ToList()
        };
        _db.OperacionesMasivas.Add(operacion);
        await _db.SaveChangesAsync();
        ViewBag.OperacionId = operacion.Id;

        ViewBag.Registrados = registrados;
        ViewBag.Omitidos    = omitidos;
        ViewBag.Errores     = errores;
        ViewBag.Resultados  = resultados;
        return View("Resultado");
    }

    private static DateTime? TryParseDate(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        string[] fmts = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy"];
        if (DateTime.TryParseExact(val, fmts,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt)) return dt;
        if (DateTime.TryParse(val, out var dt2)) return dt2;
        return null;
    }
}

public class EquipoPrevio
{
    public int     Fila           { get; set; }
    public string  NombreEquipo   { get; set; } = "";
    public string  TipoEquipo     { get; set; } = "";
    public string  Marca          { get; set; } = "";
    public string  Modelo         { get; set; } = "";
    public string  NumeroSerie    { get; set; } = "";
    public string  Accesorios     { get; set; } = "";
    public string  CostoStr       { get; set; } = "";
    public string  FechaCompraStr { get; set; } = "";
    public string  FechaGarantStr { get; set; } = "";
    public string  EstadoPreview  { get; set; } = ""; // Valido | Duplicado | NombreDuplicado | Error
    public string  MensajePreview { get; set; } = "";
}

public class ResultadoCarga
{
    public int    Fila         { get; set; }
    public string NumeroSerie  { get; set; } = "";
    public string NombreEquipo { get; set; } = "";
    public string Estado       { get; set; } = "";
    public string Mensaje      { get; set; } = "";
}
