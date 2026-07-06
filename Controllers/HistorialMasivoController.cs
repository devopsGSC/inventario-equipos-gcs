using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;

namespace InventarioTI.Controllers;

[Authorize(Roles = "Administrador,TecnicoIT")]
public class HistorialMasivoController : Controller
{
    private readonly AppDbContext _db;
    public HistorialMasivoController(AppDbContext db) => _db = db;

    // Lista de todas las operaciones con paginación y filtros
    public async Task<IActionResult> Index(
        string? tipo,
        string? usuario,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        int pagina = 1)
    {
        const int tam = 20;
        var query = _db.OperacionesMasivas.AsQueryable();

        if (!string.IsNullOrEmpty(tipo))
            query = query.Where(o => o.TipoOperacion == tipo);
        if (!string.IsNullOrEmpty(usuario))
            query = query.Where(o => o.UsuarioNombre.Contains(usuario));
        if (fechaDesde.HasValue)
            query = query.Where(o => o.FechaOperacion >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(o => o.FechaOperacion <= fechaHasta.Value.AddDays(1));

        var total = await query.CountAsync();
        var operaciones = await query
            .OrderByDescending(o => o.FechaOperacion)
            .Skip((pagina - 1) * tam)
            .Take(tam)
            .ToListAsync();

        ViewBag.Tipo        = tipo;
        ViewBag.Usuario     = usuario;
        ViewBag.FechaDesde  = fechaDesde?.ToString("yyyy-MM-dd");
        ViewBag.FechaHasta  = fechaHasta?.ToString("yyyy-MM-dd");
        ViewBag.Usuarios    = await _db.OperacionesMasivas
            .Select(o => o.UsuarioNombre).Distinct().OrderBy(u => u).ToListAsync();
        ViewBag.Paginacion  = new InventarioTI.ViewModels.PaginacionViewModel
        {
            PaginaActual   = pagina,
            TotalPaginas   = (int)Math.Ceiling(total / (double)tam),
            TotalRegistros = total,
            TamañoPagina   = tam
        };

        return View(operaciones);
    }

    // Detalle de una operación específica con todos sus registros
    public async Task<IActionResult> Detalle(int id)
    {
        var operacion = await _db.OperacionesMasivas
            .Include(o => o.Detalles)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (operacion == null) return NotFound();
        return View(operacion);
    }

    // Exportar detalle a Excel para auditoría
    public async Task<IActionResult> ExportarExcel(int id)
    {
        var operacion = await _db.OperacionesMasivas
            .Include(o => o.Detalles)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (operacion == null) return NotFound();

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Detalle");

        // Encabezado con info de la operación
        ws.Cell("A1").Value = $"Auditoría — {operacion.TipoOperacion}";
        ws.Cell("A1").Style.Font.SetBold(true).Font.SetFontSize(13);
        ws.Cell("A2").Value = $"Fecha: {operacion.FechaOperacion:dd/MM/yyyy HH:mm}";
        ws.Cell("A3").Value = $"Usuario: {operacion.UsuarioNombre}";
        ws.Cell("A4").Value = $"Archivo: {operacion.NombreArchivo}";
        ws.Cell("A5").Value = $"Resultados: {operacion.TotalExitosos} exitosos, {operacion.TotalOmitidos} omitidos, {operacion.TotalErrores} errores";

        // Encabezados de tabla
        var headers = new[] { "Fila", "NumeroSerie", "Nombre Equipo", "Estado", "Detalle", "Campos Modificados" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(7, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold(true).Font.SetFontColor(ClosedXML.Excel.XLColor.White)
                .Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromHtml("#374151"));
        }

        // Datos
        int row = 8;
        foreach (var d in operacion.Detalles.OrderBy(d => d.FilaExcel))
        {
            ws.Cell(row, 1).Value = d.FilaExcel;
            ws.Cell(row, 2).Value = d.NumeroSerie;
            ws.Cell(row, 3).Value = d.NombreEquipo ?? "";
            ws.Cell(row, 4).Value = d.Estado;
            ws.Cell(row, 5).Value = d.Mensaje ?? "";
            ws.Cell(row, 6).Value = d.CamposModificados ?? "";
            row++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var nombreArchivo = $"Auditoria_{operacion.TipoOperacion}_{operacion.FechaOperacion:yyyyMMdd_HHmm}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            nombreArchivo);
    }
}
