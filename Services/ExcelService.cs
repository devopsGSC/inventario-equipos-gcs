using ClosedXML.Excel;

namespace InventarioTI.Services;

public class ExcelService
{
    public byte[] GenerarReporteExcel(string titulo, IEnumerable<string> columnas, IEnumerable<IEnumerable<string>> filas)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Reporte");

        var cols = columnas.ToList();
        int n = cols.Count;

        // Fila 1: título mergeado
        ws.Cell(1, 1).Value = titulo;
        if (n > 1) ws.Range(1, 1, 1, n).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e2d45");
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;

        // Fila 2: encabezados con fondo gris oscuro y texto blanco
        for (int i = 0; i < n; i++)
        {
            var cell = ws.Cell(2, i + 1);
            cell.Value = cols[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#374151");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#4b5563");
        }

        // Filas 3+: datos con cebra
        int row = 3;
        bool par = false;
        foreach (var fila in filas)
        {
            var vals = fila.ToList();
            for (int i = 0; i < n; i++)
            {
                var cell = ws.Cell(row, i + 1);
                cell.Value = i < vals.Count ? vals[i] : "";
                if (par)
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FAFB");
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#e5eaf2");
            }
            row++;
            par = !par;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
