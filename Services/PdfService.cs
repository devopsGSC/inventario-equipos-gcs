using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf;
using InventarioTI.Models;

namespace InventarioTI.Services;

public class PdfService
{
    private static readonly double[] ColChars = [17.27, 11.73, 16.82, 7.0, 2.45, 16.82, 3.82, 17.45, 16.27];

    private static readonly Dictionary<int, double> RowHxl = new()
    {
        {1,15},{2,15},{3,15},{4,15},{5,15},
        {6,3.65},{7,13.9},{8,16.15},{9,4.15},
        {10,13.9},{11,13.9},{12,13.9},{13,9.0},{14,16.15},{15,3.0},
        {16,13.9},{17,13.9},{18,13.9},{19,13.9},{20,13.9},{21,8.5},
        {22,16.15},{23,9.0},{24,13.9},{25,13.9},{26,13.9},{27,13.9},{28,13.9},{29,13.9},{30,9.0},
        {31,16.15},{32,9.0},{33,13.9},{34,13.9},{35,13.9},{36,13.9},{37,9.0},
        {38,16.15},{39,16.5},{40,16.5},{41,16.5},{42,16.5},
        {43,16.15},{44,16.5},{45,18.75},{46,16.15},{47,4.9},{48,16.5},{49,16.5},
        {50,13.9},{51,16.5},{52,13.9},{53,13.9}
    };

    private const double ML  = 27;
    private const double MR  = 27;
    private const double TOP = 21;

    public byte[] GenerarDocumento(FiniquitoData d)
    {
        var doc  = new PdfDocument();
        var page = doc.AddPage();
        page.Size = PdfSharpCore.PageSize.Letter;
        var g = XGraphics.FromPdfPage(page);

        double W      = page.Width.Point;
        double usable = W - ML - MR;
        double total  = ColChars.Sum();
        double[] cp   = ColChars.Select(c => c * usable / total).ToArray();

        var fBold   = new XFont("Arial", 8,  XFontStyle.Bold);
        var fBold9  = new XFont("Arial", 9,  XFontStyle.Bold);
        var fBold10 = new XFont("Arial", 10, XFontStyle.Bold);
        var fBold16 = new XFont("Arial", 16, XFontStyle.Bold);
        var fNorm   = new XFont("Arial", 8,  XFontStyle.Regular);
        var fNorm7  = new XFont("Arial", 7,  XFontStyle.Regular);
        var pen     = XPens.Black;
        var gray    = XColor.FromArgb(209, 209, 209);

        // Precalcular top de cada fila desde arriba (Y=0 es ARRIBA en PdfSharpCore)
        // Escalar alturas para ocupar toda la página
        double H        = page.Height.Point;
        double totalRaw = Enumerable.Range(1, 53).Sum(r => (RowHxl.TryGetValue(r, out var hh) ? hh : 13.9) * 0.75);
        double available = H - TOP - 10; // 10pt margen inferior
        double scale    = available / totalRaw;

        var rowTop = new Dictionary<int, double>();
        double accum = TOP;
        for (int r = 1; r <= 60; r++)
        {
            rowTop[r] = accum;
            double h = (RowHxl.TryGetValue(r, out var hx) ? hx : 13.9) * 0.75 * scale;
            accum += h;
        }

        double Rh(int r) => (RowHxl.TryGetValue(r, out var h) ? h : 13.9) * 0.75 * scale;

        // X position of column
        (double x, double w) Cx(int c1, int c2)
        {
            double x = ML + cp[..(c1 - 1)].Sum();
            double w = cp[(c1 - 1)..c2].Sum();
            return (x, w);
        }

        // Top-Y of merged cell (PdfSharp: Y increases downward)
        double CellTop(int r1) => rowTop[r1];
        double CellH(int r1, int r2) => Enumerable.Range(r1, r2 - r1 + 1).Sum(Rh);

        // Draw rectangle: x,y is top-left in PdfSharp
        void Box(int r1, int c1, int r2, int c2, XColor? fill = null)
        {
            var (x, w) = Cx(c1, c2);
            double y = CellTop(r1);
            double h = CellH(r1, r2);
            if (fill.HasValue)
                g.DrawRectangle(new XSolidBrush(fill.Value), x, y, w, h);
            g.DrawRectangle(pen, x, y, w, h);
        }

        // Draw text centered vertically in row range
        void Txt(int r1, int r2, int c1, int c2, string? text, XFont f,
                 XStringAlignment ha = XStringAlignment.Near)
        {
            if (string.IsNullOrEmpty(text)) return;
            var (x, w) = Cx(c1, c2);
            double y = CellTop(r1);
            double h = CellH(r1, r2);
            var fmt = new XStringFormat
            {
                Alignment     = ha,
                LineAlignment = XLineAlignment.Center
            };
            double px = ha == XStringAlignment.Near ? x + 2 : x;
            double pw = ha == XStringAlignment.Near ? w - 4 : w;
            g.DrawString(text, f, XBrushes.Black, new XRect(px, y, pw, h), fmt);
        }

        void LV(int row, int lc1, int lc2, int vc1, int vc2, string label, string? val)
        {
            Txt(row, row, lc1, lc2, label, fBold);
            Txt(row, row, vc1, vc2, val,   fNorm);
        }

        void Sec(int row, string text)
        {
            Box(row, 1, row, 9, gray);
            Txt(row, row, 1, 9, text, fBold9, XStringAlignment.Center);
        }

        // ══ HEADER ══
        Box(1, 1, 5, 1);
        var (lx, lw) = Cx(1, 1);
        double logoY = CellTop(1);
        double logoH = CellH(1, 5);

        // Cargar logo desde wwwroot/images usando stream
        string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "gcs_logo.png");
        if (!File.Exists(logoPath))
            logoPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", "gcs_logo.png");

        if (File.Exists(logoPath))
        {
            try
            {
                using var logoStream = new MemoryStream(File.ReadAllBytes(logoPath));
                var img = XImage.FromStream(() => new MemoryStream(File.ReadAllBytes(logoPath)));
                double padding = 4;
                double imgW = lw - padding * 2;
                double imgH = logoH - padding * 2;
                double ratio = Math.Min(imgW / img.PixelWidth, imgH / img.PixelHeight);
                double drawW = img.PixelWidth  * ratio;
                double drawH = img.PixelHeight * ratio;
                double drawX = lx + (lw - drawW) / 2;
                double drawY = logoY + (logoH - drawH) / 2;
                g.DrawImage(img, drawX, drawY, drawW, drawH);
            }
            catch
            {
                // Fallback texto si falla la imagen
                g.DrawString("GCS", fBold16, XBrushes.Black,
                    new XRect(lx, logoY, lw, logoH),
                    new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center });
            }
        }
        else
        {
            g.DrawString("GCS", fBold16, XBrushes.Black,
                new XRect(lx, logoY, lw, logoH),
                new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center });
        }

        Box(1, 2, 1, 9);
        Txt(1, 1, 2, 9, "Entrega de equipo asignado y desvinculacion por parte del colaborador.", fBold, XStringAlignment.Center);
        Box(2, 2, 3, 7);
        Txt(2, 3, 2, 7, "Departamento de Tecnologia", fBold10, XStringAlignment.Center);
        Box(4, 2, 5, 7);
        Txt(4, 5, 2, 7, d.Titulo, fBold10, XStringAlignment.Center);

        Box(2, 8, 2, 8); Txt(2, 2, 8, 8, "Version:",         fBold);
        Box(2, 9, 2, 9); Txt(2, 2, 9, 9, "1.1",              fNorm);
        Box(3, 8, 3, 9); Txt(3, 3, 8, 9, "Fecha creacion:",   fBold);
        Box(4, 8, 4, 9); Txt(4, 4, 8, 9, "Emision:",          fBold);
        Box(5, 8, 5, 9); Txt(5, 5, 8, 9, "Fecha ultim. mod.", fBold);

        // ══ FECHA ══
        Box(6, 1, 7, 1); Txt(6, 7, 1, 1, "Fecha:", fBold);
        Box(6, 2, 7, 9); Txt(6, 7, 2, 5, d.Fecha,  fNorm);

        // ══ DATOS USUARIO ══
        Sec(8, "Datos del Usuario");
        Box(9,  1, 9, 5); Box(9,  6, 9, 9);
        Box(10, 1,10, 1); Box(10, 2,10, 5); Box(10, 6,10, 6); Box(10, 7,10, 9);
        LV(10, 1,1, 2,5, "Colaborador:", d.Colaborador);
        LV(10, 6,6, 7,9, "Centro:",      d.Centro);
        Box(11, 1,11, 1); Box(11, 2,11, 5); Box(11, 6,11, 6); Box(11, 7,11, 9);
        LV(11, 1,1, 2,5, "Area:",        d.Area);
        LV(11, 6,6, 7,9, "Sub-Area:",    d.SubArea);
        Box(12, 1,12, 1); Box(12, 2,12, 5); Box(12, 6,12, 6); Box(12, 7,12, 9);
        LV(12, 1,1, 2,5, "Cod. Empleado:",    d.CodEmpleado);
        LV(12, 6,6, 7,9, "# Identificacion:", d.Identificacion);
        Box(13, 1,13, 9);

        // ══ EQUIPO ══
        Sec(14, "Especificaciones del Equipo");
        Box(15, 1,15, 9);
        Box(16, 1,16, 1); Box(16, 2,16, 5); Box(16, 6,16, 6); Box(16, 7,16, 9);
        LV(16, 1,1, 2,5, "Tipo:",        d.Tipo);
        LV(16, 6,6, 7,9, "Marca:",       d.Marca);
        Box(17, 1,17, 1); Box(17, 2,17, 5); Box(17, 6,17, 6); Box(17, 7,17, 9);
        LV(17, 1,1, 2,5, "Modelo:",      d.Modelo);
        LV(17, 6,6, 7,9, "Service Tag:", d.ServiceTag);
        Box(18, 1,18, 1); Box(18, 2,18, 5); Box(18, 6,18, 6); Box(18, 7,18, 9);
        LV(18, 1,1, 2,5, "Memoria RAM:", d.Ram);
        LV(18, 6,6, 7,9, "Disco Duro:",  d.Disco);
        Box(19, 1,19, 1); Box(19, 2,19, 5); Box(19, 6,19, 6); Box(19, 7,19, 9);
        LV(19, 1,1, 2,5, "Procesador:",     d.Procesador);
        LV(19, 6,6, 7,9, "Fecha garantia:", d.FechaGarantia);
        Box(20, 1,20, 1); Box(20, 2,20, 5); Box(20, 6,20, 6); Box(20, 7,20, 9);
        LV(20, 1,1, 2,5, "Accesorio:", d.Accesorio);
        LV(20, 6,6, 7,9, "SKU:",       d.Sku);
        Box(21, 1,21, 9);

        // ══ PERIFÉRICOS ══
        Sec(22, "Especificaciones de Perifericos");
        Box(23, 1,23, 9);

        if (d.Perifericos.Any())
        {
            int filaP = 24;
            foreach (var p in d.Perifericos)
            {
                if (filaP > 37) break; // max 7 periféricos en las filas disponibles
                Box(filaP, 1,filaP, 1); Box(filaP, 2,filaP, 5); Box(filaP, 6,filaP, 6); Box(filaP, 7,filaP, 9);
                LV(filaP, 1,1, 2,5, $"{p.Tipo}:", $"{p.Marca} {p.Modelo}".Trim());
                LV(filaP, 6,6, 7,9, "Serie:", p.NumeroSerie);
                filaP++;
            }
            // Rellenar filas vacías restantes
            while (filaP <= 37)
            {
                Box(filaP, 1,filaP, 9);
                filaP++;
            }
        }
        else
        {
            // Sin periféricos — rellenar sección vacía
            for (int r = 24; r <= 37; r++) Box(r, 1,r, 9);
            Txt(24, 24, 1, 9, "Sin perifericos adjuntos", fNorm);
        }
        Box(29, 1,29, 9); Box(30, 1,30, 9);

        // ══ OBSERVACIONES ══
        Sec(38, "Observaciones");
        Box(39, 1,42, 9);
        if (!string.IsNullOrEmpty(d.Observaciones))
        {
            var (ox, ow) = Cx(1, 9);
            double oy = CellTop(39);
            var tf = new XTextFormatter(g);
            tf.DrawString(d.Observaciones, fNorm, XBrushes.Black,
                new XRect(ox + 3, oy + 3, ow - 6, CellH(39, 42) - 6));
        }

        // ══ MOTIVO ══
        Sec(43, "Motivo de recepcion del activo");
        Box(44, 1,45, 2); Box(44, 3,45, 5); Box(44, 6,45, 9);
        double bsz = 8;
        foreach (var (bc1, bc2, label, key) in new[]{
            (1, 2, "Renovacion",           "renovacion"),
            (3, 5, "Fin Relacion Laboral",  "fin_laboral"),
            (6, 9, "Documentacion Robo",    "robo") })
        {
            var (bx, bw) = Cx(bc1, bc2);
            double by = CellTop(44);
            double bh = CellH(44, 45);
            var cFmt = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
            g.DrawString(label, fBold, XBrushes.Black, new XRect(bx, by, bw * 0.72, bh), cFmt);
            double cbx = bx + bw * 0.76;
            double cby = by + bh / 2 - bsz / 2;
            g.DrawRectangle(pen, cbx, cby, bsz, bsz);
            if (d.Motivo == key)
                g.DrawString("X", fBold9, XBrushes.Black,
                    new XRect(cbx, cby, bsz, bsz), cFmt);
        }

        // ══ RECEPTOR ══
        Sec(46, "Receptor por parte de TI");
        Box(47, 1,47, 9);
        Box(48, 1,48, 1); Box(48, 2,48, 4); Box(48, 5,48, 6); Box(48, 7,48, 9);
        LV(48, 1,1, 2,4, "Nombre receptor:",  d.ReceptorNombre);
        LV(48, 5,6, 7,9, "Centro recepcion:", d.ReceptorCentro);
        Box(49, 1,49, 9);

        // ══ FIRMAS ══
        Box(50, 1,53, 9);
        double fw  = usable * 0.26;
        double fx1 = ML + usable * 0.04;
        double fx2 = ML + usable * 0.53;
        double firmaY  = CellTop(50);
        double firmaH  = CellH(50, 53);
        double lineY   = firmaY + firmaH * 0.45;
        g.DrawLine(pen, fx1, lineY, fx1 + fw, lineY);
        g.DrawLine(pen, fx2, lineY, fx2 + fw, lineY);
        var lFmt = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Near };
        g.DrawString("Firma de Tecnologia", fBold, XBrushes.Black,
            new XRect(fx1, lineY + 3, fw, 12), lFmt);
        g.DrawString("Firma de Empleado", fBold, XBrushes.Black,
            new XRect(fx2, lineY + 3, fw, 12), lFmt);

        // Firma digital del empleado
        if (!string.IsNullOrEmpty(d.FirmaEmpleadoBase64))
        {
            try
            {
                string b64 = d.FirmaEmpleadoBase64;
                int comma = b64.IndexOf(',');
                if (comma >= 0) b64 = b64[(comma + 1)..];
                byte[] imgBytes = Convert.FromBase64String(b64);
                var firmaImg = XImage.FromStream(() => new MemoryStream(imgBytes));
                double sigH  = firmaH * 0.38;
                double sigY  = lineY - sigH - 2;
                double ratio = Math.Min(fw / firmaImg.PixelWidth, sigH / firmaImg.PixelHeight);
                double drawW = firmaImg.PixelWidth  * ratio;
                double drawH = firmaImg.PixelHeight * ratio;
                double drawX = fx2 + (fw - drawW) / 2;
                double drawY = sigY + (sigH - drawH) / 2;
                g.DrawImage(firmaImg, drawX, drawY, drawW, drawH);
            }
            catch { /* continuar sin firma si hay error */ }
        }

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  PUNTO DE ENTRADA PÚBLICO — cada tipo llama a su propio método
    // ─────────────────────────────────────────────────────────────────────

    public byte[] GenerarCartaCompromiso(Movimiento movimiento, string? firma = null)
    {
        var eq = movimiento.Equipo!; var emp = movimiento.Empleado!;
        var perifs = (movimiento.Equipo?.EquiposPerifericos ?? [])
            .Where(ep => ep.FechaDesvinculacion == null)
            .Select(ep => new PerifericoFiniquito {
                Tipo = ep.Periferico?.TipoPeriferico?.Nombre ?? "",
                Marca = ep.Periferico?.Marca ?? "",
                Modelo = ep.Periferico?.Modelo ?? "",
                NumeroSerie = ep.Periferico?.NumeroSerie ?? ""
            }).ToList();
        return GenerarDocumentoAsignacion(new FiniquitoData
        {
            Titulo         = "Carta de Compromiso de Equipo",
            Fecha          = movimiento.FechaInicio.ToString("dd/MMM/yyyy"),
            Colaborador    = emp.Nombre,
            Centro         = emp.Departamento?.Nombre ?? "",
            Area           = emp.Cargo,
            CodEmpleado    = emp.CodigoEmpleado,
            Identificacion = emp.DUI,
            Tipo           = eq.TipoEquipo?.Nombre ?? "",
            Marca          = eq.Marca,
            Modelo         = eq.Modelo,
            ServiceTag     = eq.NumeroSerie,
            Accesorio      = eq.Accesorios ?? "",
            Sku            = eq.NumeroSerie,
            FechaGarantia  = eq.FechaGarantia?.ToString("dd/MM/yyyy") ?? "",
            Observaciones  = movimiento.Observaciones ?? "",
            Motivo         = "renovacion",
            ReceptorCentro = "GCS",
            FirmaEmpleadoBase64 = firma ?? "",
            Perifericos    = perifs
        });
    }

    public byte[] GenerarCartaPrestamo(Movimiento movimiento, string? firma = null)
    {
        var eq = movimiento.Equipo!; var emp = movimiento.Empleado!;
        string obs = movimiento.FechaFinEstimada.HasValue
            ? $"Prestamo temporal. Devolucion estimada: {movimiento.FechaFinEstimada.Value:dd/MM/yyyy}. {movimiento.Observaciones}".Trim()
            : movimiento.Observaciones ?? "";
        var perifs = (movimiento.Equipo?.EquiposPerifericos ?? [])
            .Where(ep => ep.FechaDesvinculacion == null)
            .Select(ep => new PerifericoFiniquito {
                Tipo = ep.Periferico?.TipoPeriferico?.Nombre ?? "",
                Marca = ep.Periferico?.Marca ?? "",
                Modelo = ep.Periferico?.Modelo ?? "",
                NumeroSerie = ep.Periferico?.NumeroSerie ?? ""
            }).ToList();
        return GenerarDocumentoPrestamo(new FiniquitoData
        {
            Titulo         = "Carta de Prestamo de Equipo",
            Fecha          = movimiento.FechaInicio.ToString("dd/MMM/yyyy"),
            Colaborador    = emp.Nombre,
            Centro         = emp.Departamento?.Nombre ?? "",
            Area           = emp.Cargo,
            CodEmpleado    = emp.CodigoEmpleado,
            Identificacion = emp.DUI,
            Tipo           = eq.TipoEquipo?.Nombre ?? "",
            Marca          = eq.Marca,
            Modelo         = eq.Modelo,
            ServiceTag     = eq.NumeroSerie,
            Accesorio      = eq.Accesorios ?? "",
            Sku            = eq.NumeroSerie,
            FechaGarantia  = eq.FechaGarantia?.ToString("dd/MM/yyyy") ?? "",
            Observaciones  = obs,
            Motivo         = "renovacion",
            ReceptorCentro = "GCS",
            FirmaEmpleadoBase64 = firma ?? "",
            Perifericos    = perifs
        });
    }

    public byte[] GenerarFiniquito(FiniquitoData d) => GenerarDocumentoFiniquito(d);

    // ─────────────────────────────────────────────────────────────────────
    //  PLANTILLA: ASIGNACIÓN
    //  Para modificar solo esta carta, edita este método.
    // ─────────────────────────────────────────────────────────────────────
    private byte[] GenerarDocumentoAsignacion(FiniquitoData d)
        => GenerarDocumento(d); // por ahora usa la plantilla base compartida

    // ─────────────────────────────────────────────────────────────────────
    //  PLANTILLA: PRÉSTAMO
    //  Para modificar solo esta carta, edita este método.
    // ─────────────────────────────────────────────────────────────────────
    private byte[] GenerarDocumentoPrestamo(FiniquitoData d)
        => GenerarDocumento(d); // por ahora usa la plantilla base compartida

    // ─────────────────────────────────────────────────────────────────────
    //  PLANTILLA: FINIQUITO (DEVOLUCIÓN)
    //  Para modificar solo esta carta, edita este método.
    // ─────────────────────────────────────────────────────────────────────
    private byte[] GenerarDocumentoFiniquito(FiniquitoData d)
        => GenerarDocumento(d); // por ahora usa la plantilla base compartida

}

public class FiniquitoData
{
    public string Titulo         { get; set; } = "Finiquito de Tecnologia";
    public string Fecha          { get; set; } = DateTime.Now.ToString("dd/MMM/yyyy");
    public string Colaborador    { get; set; } = "";
    public string Centro         { get; set; } = "";
    public string Area           { get; set; } = "";
    public string SubArea        { get; set; } = "";
    public string CodEmpleado    { get; set; } = "";
    public string Identificacion { get; set; } = "";
    public string Tipo           { get; set; } = "";
    public string Marca          { get; set; } = "";
    public string Modelo         { get; set; } = "";
    public string ServiceTag     { get; set; } = "";
    public string Ram            { get; set; } = "";
    public string Disco          { get; set; } = "";
    public string Procesador     { get; set; } = "";
    public string FechaGarantia  { get; set; } = "";
    public string Accesorio      { get; set; } = "";
    public string Sku            { get; set; } = "";
    public string Observaciones  { get; set; } = "";
    public string Motivo         { get; set; } = "fin_laboral";
    public string ReceptorNombre { get; set; } = "";
    public string ReceptorCentro { get; set; } = "GCS Santa Elena";
    public string FirmaEmpleadoBase64 { get; set; } = "";
    public List<PerifericoFiniquito> Perifericos { get; set; } = [];
}

public class PerifericoFiniquito
{
    public string Tipo        { get; set; } = "";
    public string Marca       { get; set; } = "";
    public string Modelo      { get; set; } = "";
    public string NumeroSerie { get; set; } = "";
}
