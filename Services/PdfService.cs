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
        // Periféricos — reducido al mínimo (header + 5 filas + spacers pequeños)
        {22,16.15},{23,3.5},{24,13.9},{25,13.9},{26,13.9},{27,13.9},{28,13.9},{29,3.5},{30,3.5},
        {31,16.15},{32,3.5},{33,13.9},{34,13.9},{35,3.5},{36,0.1},{37,0.1},
        // Observaciones
        {38,16.15},{39,16.5},{40,16.5},{41,16.5},{42,16.5},
        // Motivo
        {43,16.15},{44,16.5},{45,18.75},
        // Receptor
        {46,16.15},{47,4.9},{48,16.5},{49,16.5},
        // Firmas — grandes
        {50,24.0},{51,34.0},{52,20.0},{53,20.0}
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
        DibujarPaginaDetalle(g, page, d);
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    private void DibujarPaginaDetalle(XGraphics g, PdfPage page, FiniquitoData d, string? headerText = null)
    {
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
        Txt(1, 1, 2, 9, headerText ?? "Entrega de equipo asignado y desvinculacion.", fBold, XStringAlignment.Center);
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
                if (filaP > 28) break; // max 5 periféricos en las filas disponibles
                Box(filaP, 1,filaP, 1); Box(filaP, 2,filaP, 5); Box(filaP, 6,filaP, 6); Box(filaP, 7,filaP, 9);
                LV(filaP, 1,1, 2,5, $"{p.Tipo}:", $"{p.Marca} {p.Modelo}".Trim());
                LV(filaP, 6,6, 7,9, "Serie:", p.NumeroSerie);
                filaP++;
            }
            // Rellenar filas vacías restantes
            while (filaP <= 28)
            {
                Box(filaP, 1,filaP, 9);
                filaP++;
            }
        }
        else
        {
            // Sin periféricos — rellenar sección vacía
            for (int r = 24; r <= 28; r++) Box(r, 1,r, 9);
            Txt(24, 24, 1, 9, "Sin perifericos adjuntos", fNorm);
        }
        Box(29, 1,29, 9); Box(30, 1,30, 9);

        // ══ TELÉFONO MÓVIL ══
        Sec(31, "Especificaciones del Telefono Movil");
        Box(32, 1,32, 9);
        Box(33, 1,33, 1); Box(33, 2,33, 5); Box(33, 6,33, 6); Box(33, 7,33, 9);
        LV(33, 1,1, 2,5, "Numero:", d.TelNumero);
        LV(33, 6,6, 7,9, "Marca:",  d.TelMarca);
        Box(34, 1,34, 1); Box(34, 2,34, 5); Box(34, 6,34, 6); Box(34, 7,34, 9);
        LV(34, 1,1, 2,5, "Modelo:", d.TelModelo);
        LV(34, 6,6, 7,9, "IMEI:",   d.TelImei);
        Box(35, 1,35, 9);

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
        double lineY   = firmaY + firmaH * 0.78;  // líneas más abajo
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
                // Usar casi todo el espacio disponible entre el tope y la línea
                double espacioDisp = lineY - firmaY - 6;
                double ratio = Math.Min(fw / firmaImg.PixelWidth, espacioDisp / firmaImg.PixelHeight);
                double drawW = firmaImg.PixelWidth  * ratio;
                double drawH = firmaImg.PixelHeight * ratio;
                double drawX = fx2 + (fw - drawW) / 2;
                double drawY = firmaY + (espacioDisp - drawH) / 2 + 3;
                g.DrawImage(firmaImg, drawX, drawY, drawW, drawH);
            }
            catch { /* continuar sin firma si hay error */ }
        }

        // Firma IT (responsable de tecnología)
        if (!string.IsNullOrEmpty(d.RutaFirmaIT))
        {
            try
            {
                string rutaFisica = Path.Combine(
                    Directory.GetCurrentDirectory(), "wwwroot",
                    d.RutaFirmaIT.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(rutaFisica))
                {
                    var firmaITImg = XImage.FromStream(() =>
                        new MemoryStream(File.ReadAllBytes(rutaFisica)));
                    double itH = firmaH * 0.55;
                    double ratio = Math.Min(fw / firmaITImg.PixelWidth, itH / firmaITImg.PixelHeight);
                    double dw = firmaITImg.PixelWidth  * ratio;
                    double dh = firmaITImg.PixelHeight * ratio;
                    g.DrawImage(firmaITImg, fx1 + (fw - dw) / 2, lineY - dh - 4, dw, dh);
                }
            }
            catch { /* continuar sin firma IT si falla */ }
        }
    } // fin DibujarPaginaDetalle

    // ─────────────────────────────────────────────────────────────────────
    //  REPORTE TABULAR MULTI-PÁGINA (landscape)
    // ─────────────────────────────────────────────────────────────────────
    public byte[] GenerarReportePdf(
        string titulo,
        IEnumerable<string> columnas,
        IEnumerable<IEnumerable<string>> filas,
        string? subtitulo = null)
    {
        var doc      = new PdfDocument();
        var cols     = columnas.ToList();
        var filaList = filas.Select(f => f.ToList()).ToList();

        const double ml = 20, mr = 20, mt = 16;
        var fTitle   = new XFont("Arial", 11, XFontStyle.Bold);
        var fSub     = new XFont("Arial", 7,  XFontStyle.Regular);
        var fHead    = new XFont("Arial", 6.5, XFontStyle.Bold);
        var fData    = new XFont("Arial", 6.5, XFontStyle.Regular);
        var headerBg = XColor.FromArgb(30, 45, 69);
        var stripeBg = XColor.FromArgb(249, 250, 251);
        var linePen  = new XPen(XColor.FromArgb(220, 222, 228), 0.3);
        const double rowH  = 12.5;
        const double headH = 14;

        PdfPage page   = null!;
        XGraphics g    = null!;
        double y       = 0;
        double W       = 0, usable = 0;
        double[] cw    = [];

        void NewPage()
        {
            page = doc.AddPage();
            page.Size        = PdfSharpCore.PageSize.Letter;
            page.Orientation = PdfSharpCore.PageOrientation.Landscape;
            g       = XGraphics.FromPdfPage(page);
            W       = page.Width.Point;
            usable  = W - ml - mr;
            cw      = Enumerable.Repeat(usable / cols.Count, cols.Count).ToArray();
            y       = mt;

            // Logo
            string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "gcs_logo.png");
            if (File.Exists(logoPath))
            {
                try
                {
                    var img = XImage.FromStream(() => new MemoryStream(File.ReadAllBytes(logoPath)));
                    double lh = 22, lw = img.PixelWidth * (lh / img.PixelHeight);
                    g.DrawImage(img, ml, y, lw, lh);
                }
                catch { }
            }

            // Título centrado
            var fmtC = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
            g.DrawString(titulo, fTitle, XBrushes.Black, new XRect(ml, y, usable, 22), fmtC);
            y += 26;

            // Subtítulo (fecha + filtros)
            string sub = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
            if (!string.IsNullOrWhiteSpace(subtitulo)) sub += $"   |   {subtitulo}";
            var fmtL = new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Center };
            g.DrawString(sub, fSub, XBrushes.Gray, new XRect(ml, y, usable, 10), fmtL);
            y += 13;

            DrawHeaders();
        }

        void DrawHeaders()
        {
            double x = ml;
            for (int i = 0; i < cols.Count; i++)
            {
                g.DrawRectangle(new XSolidBrush(headerBg), x, y, cw[i], headH);
                var fmt = new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Center };
                g.DrawString(cols[i], fHead, XBrushes.White, new XRect(x + 2, y, cw[i] - 4, headH), fmt);
                x += cw[i];
            }
            y += headH;
        }

        NewPage();

        bool stripe = false;
        foreach (var fila in filaList)
        {
            if (y > page.Height.Point - 60)
            {
                NewPage();
                stripe = false;
            }

            double x = ml;
            var bg = stripe ? new XSolidBrush(stripeBg) : new XSolidBrush(XColors.White);
            for (int i = 0; i < cols.Count; i++)
            {
                g.DrawRectangle(bg, x, y, cw[i], rowH);
                g.DrawRectangle(linePen, x, y, cw[i], rowH);
                string val = i < fila.Count ? (fila[i] ?? "") : "";
                var fmt = new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Center };
                g.DrawString(val, fData, XBrushes.Black, new XRect(x + 2, y, cw[i] - 4, rowH), fmt);
                x += cw[i];
            }
            y += rowH;
            stripe = !stripe;
        }

        if (filaList.Count == 0)
        {
            var fmt = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
            g.DrawString("Sin datos para mostrar", fData, XBrushes.Gray,
                new XRect(ml, y, usable, rowH * 2), fmt);
        }

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  PUNTO DE ENTRADA PÚBLICO — cada tipo llama a su propio método
    // ─────────────────────────────────────────────────────────────────────

    public byte[] GenerarCartaCompromiso(Movimiento movimiento, string? firma = null, string? rutaFirmaIT = null)
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
            RutaFirmaIT    = rutaFirmaIT,
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
            TelImei        = eq.IMEI ?? "",
            FirmaEmpleadoBase64 = firma ?? "",
            Perifericos    = perifs
        });
    }

    public byte[] GenerarCartaPrestamo(Movimiento movimiento, string? firma = null, string? rutaFirmaIT = null)
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
            RutaFirmaIT    = rutaFirmaIT,
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
            TelImei        = eq.IMEI ?? "",
            FirmaEmpleadoBase64 = firma ?? "",
            Perifericos    = perifs
        });
    }

    public byte[] GenerarFiniquito(FiniquitoData d) => GenerarDocumentoFiniquito(d);

    // ─────────────────────────────────────────────────────────────────────
    //  HALLAZGOS Y ESTADO DEL EQUIPO/PERIFÉRICO — documento independiente
    // ─────────────────────────────────────────────────────────────────────
    public byte[] GenerarPdfHallazgos(Movimiento movimiento, string? rutaFirmaIT = null)
    {
        var eq  = movimiento.Equipo!;
        var emp = movimiento.Empleado;
        var tipoLabel = movimiento.TipoMovimiento switch
        {
            "Asignacion" => "Asignación", "Prestamo" => "Préstamo",
            "Devolucion" => "Devolución", _ => movimiento.TipoMovimiento
        };

        var infoLineas = new List<(string label, string valor)>
        {
            ("Tipo de movimiento:", tipoLabel),
            ("Fecha:",              movimiento.FechaInicio.ToString("dd/MM/yyyy HH:mm")),
            ("Equipo:",             $"{eq.NombreEquipo} — {eq.Marca} {eq.Modelo}"),
            ("Número de serie:",    eq.NumeroSerie),
        };
        if (!string.IsNullOrEmpty(eq.IMEI))
            infoLineas.Add(("IMEI:", eq.IMEI));
        if (emp != null)
            infoLineas.Add(("Colaborador:", $"{emp.Nombre} ({emp.CodigoEmpleado}) — {emp.Departamento?.Nombre}"));
        if (movimiento.Sitio != null)
            infoLineas.Add(("Sitio:", movimiento.Sitio.Nombre));
        if (!string.IsNullOrEmpty(movimiento.Observaciones))
            infoLineas.Add(("Observaciones:", movimiento.Observaciones));

        return GenerarPdfHallazgosInterno(infoLineas, movimiento.Imagenes.OrderBy(i => i.Orden),
            rutaFirmaIT, emp?.Nombre ?? "Colaborador");
    }

    public byte[] GenerarPdfHallazgosPeriferico(EquipoPeriferico ep, string? rutaFirmaIT = null)
    {
        var per = ep.Periferico!;
        var emp = ep.Empleado;
        var tipoLabel = ep.TipoMovimiento switch
        {
            "Asignacion" => "Asignación", "Prestamo" => "Préstamo",
            "Devolucion" => "Devolución", _ => ep.TipoMovimiento
        };

        var infoLineas = new List<(string label, string valor)>
        {
            ("Tipo de movimiento:", tipoLabel),
            ("Fecha:",              ep.FechaAsignacion.ToString("dd/MM/yyyy HH:mm")),
            ("Periférico:",         $"{per.TipoPeriferico?.Nombre} — {per.Marca} {per.Modelo}"),
            ("Número de serie:",    per.NumeroSerie),
        };
        if (emp != null)
            infoLineas.Add(("Colaborador:", $"{emp.Nombre} ({emp.CodigoEmpleado}) — {emp.Departamento?.Nombre}"));
        if (ep.Sitio != null)
            infoLineas.Add(("Sitio:", ep.Sitio.Nombre));
        if (!string.IsNullOrEmpty(ep.Observaciones))
            infoLineas.Add(("Observaciones:", ep.Observaciones));

        return GenerarPdfHallazgosInterno(infoLineas, ep.Imagenes.OrderBy(i => i.Orden),
            rutaFirmaIT, emp?.Nombre ?? "Colaborador");
    }

    private byte[] GenerarPdfHallazgosInterno(List<(string label, string valor)> infoLineas,
        IEnumerable<ImagenMovimiento> imagenesOrdenadas, string? rutaFirmaIT, string nombreColaborador)
    {
        var doc  = new PdfDocument();
        var page = doc.AddPage();
        page.Size = PdfSharpCore.PageSize.Letter;
        var g = XGraphics.FromPdfPage(page);

        double W  = page.Width.Point;
        double H  = page.Height.Point;
        double ML = 48, MR = 48, MT = 40;
        double TW = W - ML - MR;

        var fTitle  = new XFont("Arial", 12, XFontStyle.Bold);
        var fBold   = new XFont("Arial", 10, XFontStyle.Bold);
        var fNorm   = new XFont("Arial", 9,  XFontStyle.Regular);
        var fSm     = new XFont("Arial", 8,  XFontStyle.Regular);
        var gray    = XColor.FromArgb(209, 209, 209);
        var fmtC    = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Near };

        double y = MT;

        // ── Logo ──
        string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "gcs_logo.png");
        if (File.Exists(logoPath))
        {
            try
            {
                var img = XImage.FromStream(() => new MemoryStream(File.ReadAllBytes(logoPath)));
                double lw = 70, lh = img.PixelHeight * (70.0 / img.PixelWidth);
                g.DrawImage(img, ML, y, lw, lh);
                y += lh + 6;
            }
            catch { y += 28; }
        }

        // ── Título ──
        g.DrawString("HALLAZGOS Y ESTADO DEL EQUIPO", fTitle, XBrushes.Black,
            new XRect(ML, y, TW, 18), fmtC);
        y += 22;

        g.DrawLine(new XPen(gray, 0.5), ML, y, ML + TW, y);
        y += 10;

        // ── Info del movimiento — una columna ──
        double labelW = 105;
        double valueX = ML + labelW;
        double valueW = TW - labelW;

        foreach (var (label, valor) in infoLineas)
        {
            g.DrawString(label, fBold, XBrushes.Black, ML, y + fBold.Size);

            // Word wrap manual para el valor si es muy largo
            var palabras = valor.Split(' ');
            string lineaActual = "";

            foreach (var palabra in palabras)
            {
                var prueba = lineaActual.Length == 0 ? palabra : lineaActual + " " + palabra;
                var size   = g.MeasureString(prueba, fNorm);
                if (size.Width > valueW && lineaActual.Length > 0)
                {
                    g.DrawString(lineaActual, fNorm, XBrushes.Black, valueX, y + fNorm.Size);
                    y += 13;
                    lineaActual = palabra;
                }
                else
                {
                    lineaActual = prueba;
                }
            }
            if (lineaActual.Length > 0)
                g.DrawString(lineaActual, fNorm, XBrushes.Black, valueX, y + fNorm.Size);

            y += 16; // espacio entre filas de info
        }

        y += 6;
        g.DrawLine(new XPen(gray, 0.5), ML, y, ML + TW, y);
        y += 14;

        // ── Sección de imágenes ──
        g.DrawString("REGISTRO FOTOGRÁFICO", fBold, XBrushes.Black, ML, y + 10);
        y += 18;

        var listaImagenes = imagenesOrdenadas.ToList();
        int totalFotos = listaImagenes.Count;

        const double imgW = 220, gapX = 20;
        double imgH = totalFotos <= 4 ? 155 : 160;
        double gapY = totalFotos <= 4 ? 22 : 30;

        int    filas         = (int)Math.Ceiling(totalFotos / 2.0);
        double alturaFotos   = filas * (imgH + gapY);
        double alturaFirmas  = 80; // espacio para líneas de firma
        double alturaTotal   = y + alturaFotos + alturaFirmas + 30; // 30 = margen
        bool   cabeEnUnaPagina = totalFotos <= 4 && alturaTotal <= H - 60;

        double col1X = ML;
        double col2X = ML + imgW + gapX;
        bool   esCol1 = true;

        foreach (var imagen in listaImagenes)
        {
            if (y + imgH + gapY > H - 60)
            {
                DibujarPiePagina(g, W, H, ML, TW, gray, fSm);
                page = doc.AddPage();
                page.Size = PdfSharpCore.PageSize.Letter;
                g = XGraphics.FromPdfPage(page);
                y = MT;
                esCol1 = true;
            }

            double imgX = esCol1 ? col1X : col2X;

            try
            {
                string rutaFisica = Path.Combine(
                    Directory.GetCurrentDirectory(), "wwwroot",
                    imagen.RutaImagen.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(rutaFisica))
                {
                    var imgObj = XImage.FromStream(() => new MemoryStream(File.ReadAllBytes(rutaFisica)));
                    double ratio = Math.Min(imgW / imgObj.PixelWidth, imgH / imgObj.PixelHeight);
                    double dw = imgObj.PixelWidth  * ratio;
                    double dh = imgObj.PixelHeight * ratio;
                    double dx = imgX + (imgW - dw) / 2;
                    double dy = y    + (imgH - dh) / 2;

                    g.DrawRectangle(new XPen(gray, 0.5), imgX, y, imgW, imgH);
                    g.DrawImage(imgObj, dx, dy, dw, dh);
                }
                else
                {
                    g.DrawRectangle(new XPen(gray, 0.5), imgX, y, imgW, imgH);
                    g.DrawString("Imagen no disponible", fSm, XBrushes.Gray,
                        new XRect(imgX, y, imgW, imgH), fmtC);
                }
            }
            catch
            {
                g.DrawRectangle(new XPen(gray, 0.5), imgX, y, imgW, imgH);
                g.DrawString("Error al cargar imagen", fSm, XBrushes.Gray,
                    new XRect(imgX, y, imgW, imgH), fmtC);
            }

            g.DrawString($"Foto {imagen.Orden}", fBold, XBrushes.Black, imgX, y + imgH + 4);
            if (!string.IsNullOrEmpty(imagen.Descripcion))
                g.DrawString(imagen.Descripcion, fSm, XBrushes.Gray, imgX, y + imgH + 14);

            if (!esCol1)
                y += imgH + gapY;
            esCol1 = !esCol1;
        }

        if (!esCol1) y += imgH + gapY;

        // ── Sección de firmas ──
        y += 16;
        // Solo saltar página si NO cabe en una sola página
        if (!cabeEnUnaPagina && y + 80 > H - 60)
        {
            DibujarPiePagina(g, W, H, ML, TW, gray, fSm);
            page = doc.AddPage();
            page.Size = PdfSharpCore.PageSize.Letter;
            g = XGraphics.FromPdfPage(page);
            y = MT;
        }

        g.DrawLine(new XPen(gray, 0.5), ML, y, ML + TW, y);
        y += 16;

        double fw   = TW * 0.38;
        double fx1  = ML;
        double fx2  = ML + TW - fw;
        double lineY = y + 44;

        if (!string.IsNullOrEmpty(rutaFirmaIT))
        {
            try
            {
                string rutaFisica = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                    rutaFirmaIT.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(rutaFisica))
                {
                    var firmaIT = XImage.FromStream(() => new MemoryStream(File.ReadAllBytes(rutaFisica)));
                    double ratio = Math.Min(fw / firmaIT.PixelWidth, 40.0 / firmaIT.PixelHeight);
                    double dw = firmaIT.PixelWidth * ratio, dh = firmaIT.PixelHeight * ratio;
                    g.DrawImage(firmaIT, fx1 + (fw - dw) / 2, lineY - dh - 2, dw, dh);
                }
            }
            catch { }
        }

        g.DrawLine(XPens.Black, fx1, lineY, fx1 + fw, lineY);
        g.DrawLine(XPens.Black, fx2, lineY, fx2 + fw, lineY);

        var fmtCtr = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Near };
        g.DrawString("Firma Responsable TI", fSm, XBrushes.Black, new XRect(fx1, lineY + 4, fw, 12), fmtCtr);
        g.DrawString(nombreColaborador, fBold, XBrushes.Black, new XRect(fx2, lineY + 4, fw, 12), fmtCtr);
        g.DrawString("Firma del Colaborador", fSm, XBrushes.Black, new XRect(fx2, lineY + 14, fw, 12), fmtCtr);

        DibujarPiePagina(g, W, H, ML, TW, gray, fSm);

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    private void DibujarPiePagina(XGraphics g, double W, double H, double ML, double TW,
        XColor gray, XFont fSm)
    {
        g.DrawLine(new XPen(gray, 0.5), ML, H - 30, ML + TW, H - 30);
        var fmtC = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Near };
        g.DrawString(
            "Global Customs Solutions S.E.M. de C.V.  |  Departamento de Tecnología  |  Documento confidencial",
            fSm, XBrushes.Gray, new XRect(ML, H - 24, TW, 12), fmtC);
    }

    public byte[] GenerarCartaCompromisoPerifericos(EquipoPeriferico ep, string? rutaFirmaIT = null)
    {
        var doc  = new PdfDocument();
        var page = doc.AddPage();
        page.Size = PdfSharpCore.PageSize.Letter;
        var g = XGraphics.FromPdfPage(page);
        DibujarCartaPeriferico(g, page, ep, rutaFirmaIT);
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    private void DibujarCartaPeriferico(XGraphics g, PdfPage page, EquipoPeriferico ep, string? rutaFirmaIT = null)
    {
        var emp = ep.Empleado!;
        var per = ep.Periferico!;

        double W  = page.Width.Point;
        double H  = page.Height.Point;
        double ML = 54, MR = 54, MT = 36;
        double TW = W - ML - MR;

        var fTitle   = new XFont("Arial", 12,   XFontStyle.Bold);
        var fBold    = new XFont("Arial", 10.5, XFontStyle.Bold);
        var fBoldSig = new XFont("Arial", 8,    XFontStyle.Bold);
        var fNorm    = new XFont("Arial", 10.5, XFontStyle.Regular);
        var fSm      = new XFont("Arial", 9.5,  XFontStyle.Regular);
        var gray     = XColor.FromArgb(209, 209, 209);

        double y       = MT;
        double leading = 15.5;

        List<string> WordWrap(string text, XFont font, double maxW)
        {
            var lines   = new List<string>();
            var words   = text.Split(' ');
            var current = "";
            foreach (var word in words)
            {
                var test = current.Length == 0 ? word : current + " " + word;
                if (g.MeasureString(test, font).Width <= maxW)
                    current = test;
                else
                {
                    if (current.Length > 0) lines.Add(current);
                    current = word;
                }
            }
            if (current.Length > 0) lines.Add(current);
            return lines;
        }

        double DrawPara(string text, XFont font, double indentL = 0, double spaceAfter = 10)
        {
            foreach (var line in WordWrap(text, font, TW - indentL))
            {
                g.DrawString(line, font, XBrushes.Black, ML + indentL, y + font.Size);
                y += leading;
            }
            y += spaceAfter;
            return y;
        }

        // ── Logo ──
        string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "gcs_logo.png");
        if (File.Exists(logoPath))
        {
            try
            {
                var img = XImage.FromStream(() => new MemoryStream(File.ReadAllBytes(logoPath)));
                double lw = 75, lh = img.PixelHeight * (75.0 / img.PixelWidth);
                g.DrawImage(img, ML, y, lw, lh);
                y += lh + 8;
            }
            catch { y += 28; }
        }
        else { y += 28; }

        // ── Título ──
        string titulo = ep.TipoMovimiento == "Prestamo"
            ? "CARTA DE PRÉSTAMO DE PERIFÉRICO TECNOLÓGICO"
            : "CARTA DE COMPROMISO DE PERIFÉRICO TECNOLÓGICO";
        var fmtC = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Near };
        g.DrawString(titulo, fTitle, XBrushes.Black,
            new XRect(ML, y, TW, 16), fmtC);
        y += 20;

        g.DrawLine(new XPen(gray, 0.5), ML, y, ML + TW, y);
        y += 10;

        // ── Fecha ──
        var fmtR = new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Near };
        g.DrawString($"San Salvador, {ep.FechaAsignacion:dd/MMM/yyyy}", fSm, XBrushes.Black,
            new XRect(ML, y, TW, 12), fmtR);
        y += 18;

        // ── Cuerpo ──
        DrawPara($"Yo, {emp.Nombre}, portador(a) del DUI {emp.DUI}, quien desempeña el cargo de {emp.Cargo}, por este medio hago constar que recibo de Global Customs Solutions S.E.M. de C.V. el siguiente periférico corporativo en buenas condiciones de funcionamiento:", fNorm);

        string tipo = per.TipoPeriferico?.Nombre ?? "";
        DrawPara($"{tipo}: {per.Marca} {per.Modelo} — S/N: {per.NumeroSerie}", fBold, indentL: 12, spaceAfter: 10);

        DrawPara("Asimismo, declaro que recibo dicho bien con sus respectivos accesorios y me comprometo a utilizarlo exclusivamente para fines laborales, así como a devolverlo en buen estado al momento que la empresa lo solicite o al finalizar mi relación laboral.", fNorm);
        DrawPara("Cualquier daño, desperfecto, pérdida, extravío o robo que sufriere el periférico después de su asignación será bajo mi responsabilidad, comprometiéndome a asumir el costo de la reparación correspondiente o el valor deducible necesario para la reposición del bien por uno de características similares, salvo deterioro ocasionado por uso normal o fin de vida útil.", fNorm);
        DrawPara("En caso de identificar fallas de fábrica o desperfectos al momento de la recepción, me comprometo a reportarlos inmediatamente al Departamento de Tecnología para la validación y aplicación de garantía correspondiente. De no reportarse oportunamente, se entenderá que el periférico fue recibido a satisfacción.", fNorm);
        DrawPara("Reconozco que la no devolución del periférico corporativo o cualquier saldo pendiente derivado de su uso podrá dar lugar a acciones administrativas o disciplinarias conforme a las políticas internas de la empresa.", fNorm);
        DrawPara("Asimismo, manifiesto que tengo conocimiento de las siguientes disposiciones:", fNorm, spaceAfter: 6);
        DrawPara("- Está prohibida la instalación de software, aplicaciones o herramientas no autorizadas por el Departamento de Tecnología.", fNorm, indentL: 8, spaceAfter: 5);
        DrawPara("- No está permitido realizar configuraciones que comprometan la seguridad de la información corporativa.", fNorm, indentL: 8, spaceAfter: 5);
        DrawPara("- Me comprometo a seguir las políticas de seguridad informática y las buenas prácticas establecidas por la empresa, especialmente en el uso de redes públicas, navegación en internet y manejo de información confidencial.", fNorm, indentL: 8, spaceAfter: 10);
        DrawPara("Finalmente, me comprometo a cuidar y hacer buen uso del bien asignado, contribuyendo a prolongar su vida útil y garantizando su adecuada conservación.", fNorm);

        // ── Pie de página ──
        g.DrawLine(new XPen(gray, 0.5), ML, H - 30, ML + TW, H - 30);
        var fmtPie = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Near };
        g.DrawString("Global Customs Solutions S.E.M. de C.V.  |  Departamento de Tecnología  |  Documento confidencial",
            new XFont("Arial", 7, XFontStyle.Regular), XBrushes.Gray,
            new XRect(ML, H - 22, TW, 12), fmtPie);

        // ── Firmas ──
        double fw       = TW * 0.26;
        double fx1      = ML + TW * 0.04;
        double fx2      = ML + TW * 0.53;
        double sigTop   = H - 90;
        double lineY    = H - 52;

        g.DrawLine(XPens.Black, fx1, lineY, fx1 + fw, lineY);
        g.DrawLine(XPens.Black, fx2, lineY, fx2 + fw, lineY);
        var lFmt = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Near };
        g.DrawString("Firma de Tecnología", fBoldSig, XBrushes.Black, new XRect(fx1, lineY + 3, fw, 12), lFmt);
        g.DrawString("Firma de Empleado",   fBoldSig, XBrushes.Black, new XRect(fx2, lineY + 3, fw, 12), lFmt);

        // Firma digital del empleado
        if (!string.IsNullOrEmpty(ep.FirmaEmpleado))
        {
            try
            {
                string b64 = ep.FirmaEmpleado;
                int comma = b64.IndexOf(',');
                if (comma >= 0) b64 = b64[(comma + 1)..];
                byte[] imgBytes = Convert.FromBase64String(b64);
                var firmaImg = XImage.FromStream(() => new MemoryStream(imgBytes));
                double espacioDisp = lineY - sigTop - 6;
                double ratio = Math.Min(fw / firmaImg.PixelWidth, espacioDisp / firmaImg.PixelHeight);
                double drawW = firmaImg.PixelWidth  * ratio;
                double drawH = firmaImg.PixelHeight * ratio;
                double drawX = fx2 + (fw - drawW) / 2;
                double drawY = sigTop + (espacioDisp - drawH) / 2 + 3;
                g.DrawImage(firmaImg, drawX, drawY, drawW, drawH);
            }
            catch { /* continuar sin firma si hay error */ }
        }

        // Firma IT (responsable de tecnología)
        if (!string.IsNullOrEmpty(rutaFirmaIT))
        {
            try
            {
                string rutaFisica = Path.Combine(
                    Directory.GetCurrentDirectory(), "wwwroot",
                    rutaFirmaIT.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(rutaFisica))
                {
                    var firmaITImg = XImage.FromStream(() =>
                        new MemoryStream(File.ReadAllBytes(rutaFisica)));
                    double espacioDisp = lineY - sigTop - 6;
                    double ratio = Math.Min(fw / firmaITImg.PixelWidth, espacioDisp / firmaITImg.PixelHeight);
                    double dw = firmaITImg.PixelWidth  * ratio;
                    double dh = firmaITImg.PixelHeight * ratio;
                    double dx = fx1 + (fw - dw) / 2;
                    double dy = sigTop + (espacioDisp - dh) / 2 + 3;
                    g.DrawImage(firmaITImg, dx, dy, dw, dh);
                }
            }
            catch { /* continuar sin firma IT si falla */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  CARA FRONTAL — carta legal de compromiso/préstamo
    // ─────────────────────────────────────────────────────────────────────
    private void GenerarCaraFrontal(PdfDocument doc, FiniquitoData d, bool esPrestamo)
    {
        var page = doc.AddPage();
        page.Size = PdfSharpCore.PageSize.Letter;
        var g = XGraphics.FromPdfPage(page);

        double W  = page.Width.Point;
        double H  = page.Height.Point;
        double ML = 54, MR = 54, MT = 36;
        double TW = W - ML - MR;

        var fTitle = new XFont("Arial", 12, XFontStyle.Bold);
        var fBold  = new XFont("Arial", 10.5, XFontStyle.Bold);
        var fNorm  = new XFont("Arial", 10.5, XFontStyle.Regular);
        var fSm    = new XFont("Arial", 9.5, XFontStyle.Regular);
        var gray   = XColor.FromArgb(209, 209, 209);

        double y = MT;
        double leading = 15.5;  // interlineado uniforme para todo el texto

        // ── Función de word wrap manual ──────────────────────────────
        // Divide un texto en líneas que caben en el ancho dado
        List<string> WordWrap(string text, XFont font, double maxW)
        {
            var lines  = new List<string>();
            var words  = text.Split(' ');
            var current = "";
            foreach (var word in words)
            {
                var test = current.Length == 0 ? word : current + " " + word;
                var size = g.MeasureString(test, font);
                if (size.Width <= maxW)
                    current = test;
                else
                {
                    if (current.Length > 0) lines.Add(current);
                    current = word;
                }
            }
            if (current.Length > 0) lines.Add(current);
            return lines;
        }

        // Dibuja un párrafo con word wrap y devuelve la Y final
        double DrawPara(string text, XFont font, double indentL = 0, double spaceAfter = 10)
        {
            var lines = WordWrap(text, font, TW - indentL);
            foreach (var line in lines)
            {
                g.DrawString(line, font, XBrushes.Black, ML + indentL, y + font.Size);
                y += leading;
            }
            y += spaceAfter;
            return y;
        }

        // ── Logo ──────────────────────────────────────────────────────
        string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "gcs_logo.png");
        if (File.Exists(logoPath))
        {
            try
            {
                var img = XImage.FromStream(() => new MemoryStream(File.ReadAllBytes(logoPath)));
                double lw = 75, lh = img.PixelHeight * (75.0 / img.PixelWidth);
                g.DrawImage(img, ML, y, lw, lh);
                y += lh + 8;
            }
            catch { y += 28; }
        }
        else { y += 28; }

        // ── Título ────────────────────────────────────────────────────
        string titulo = esPrestamo
            ? "CARTA DE PRÉSTAMO DE EQUIPO TECNOLÓGICO"
            : "CARTA DE COMPROMISO DE EQUIPO TECNOLÓGICO";
        var fmtC = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Near };
        g.DrawString(titulo, fTitle, XBrushes.Black, new XRect(ML, y, TW, 16), fmtC);
        y += 20;

        g.DrawLine(new XPen(gray, 0.5), ML, y, ML + TW, y);
        y += 10;

        // ── Fecha ─────────────────────────────────────────────────────
        var fmtR = new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Near };
        g.DrawString($"San Salvador, {d.Fecha}", fSm, XBrushes.Black, new XRect(ML, y, TW, 12), fmtR);
        y += 18;

        // ── Descripción del equipo en lista vertical ─────────────────
        void DrawEquipoLista()
        {
            var items = new List<string>();
            string eqLine = $"Equipo: {d.Tipo} {d.Marca} {d.Modelo}".Trim();
            if (!string.IsNullOrEmpty(d.ServiceTag)) eqLine += $" (S/N: {d.ServiceTag})";
            items.Add(eqLine);
            if (!string.IsNullOrEmpty(d.Accesorio))
                items.Add($"Accesorios: {d.Accesorio}");
            foreach (var p in d.Perifericos)
                items.Add($"{p.Tipo}: {p.Marca} {p.Modelo} — S/N: {p.NumeroSerie}");
            for (int i = 0; i < items.Count; i++)
                DrawPara(items[i], fBold, indentL: 12, spaceAfter: i < items.Count - 1 ? 3 : 10);
        }

        // ── Cuerpo del texto ──────────────────────────────────────────
        if (!esPrestamo)
        {
            DrawPara($"Yo, {d.Colaborador}, portador(a) del Documento Único de Identidad número {d.Identificacion}, quien desempeña el cargo de {d.Area}, por este medio hago constar que recibo de Global Customs Solutions S.E.M. de C.V. el siguiente equipo y/o accesorios corporativos, en buenas condiciones de funcionamiento:", fNorm);

            DrawEquipoLista();

            DrawPara("Asimismo, declaro que recibo dichos bienes con sus respectivos accesorios y me comprometo a utilizarlos exclusivamente para fines laborales, así como a devolverlos en buen estado al momento que la empresa lo solicite o al finalizar mi relación laboral.", fNorm);

            DrawPara("Cualquier daño, desperfecto, pérdida, extravío o robo que sufriere el equipo o accesorios después de su asignación será bajo mi responsabilidad, comprometiéndome a asumir el costo de la reparación correspondiente o el valor deducible necesario para la reposición del bien por uno de características similares, salvo deterioro ocasionado por uso normal o fin de vida útil del equipo.", fNorm);

            DrawPara("En caso de identificar fallas de fábrica o desperfectos al momento de la recepción, me comprometo a reportarlos inmediatamente al Departamento de Tecnología para la validación y aplicación de garantía correspondiente. De no reportarse oportunamente, se entenderá que el equipo fue recibido a satisfacción.", fNorm);

            DrawPara("Reconozco que la no devolución del equipo corporativo, accesorios asignados o cualquier saldo pendiente derivado de su uso podrá dar lugar a acciones administrativas o disciplinarias conforme a las políticas internas de la empresa.", fNorm);

            DrawPara("Asimismo, manifiesto que tengo conocimiento de las siguientes disposiciones:", fNorm, spaceAfter: 6);

            DrawPara("- Está prohibida la instalación de software, aplicaciones o herramientas no autorizadas por el Departamento de Tecnología.", fNorm, indentL: 8, spaceAfter: 5);
            DrawPara("- No está permitido realizar configuraciones que comprometan la seguridad de la información corporativa.", fNorm, indentL: 8, spaceAfter: 5);
            DrawPara("- En el caso de teléfonos corporativos, cualquier cargo adicional al plan asignado, incluyendo suscripciones, promociones, compras, mensajes premium o consumos no autorizados, será responsabilidad exclusiva del usuario.", fNorm, indentL: 8, spaceAfter: 5);
            DrawPara("- Me comprometo a seguir las políticas de seguridad informática y las buenas prácticas establecidas por la empresa, especialmente en el uso de redes públicas, navegación en internet y manejo de información confidencial.", fNorm, indentL: 8, spaceAfter: 10);

            DrawPara("Finalmente, me comprometo a cuidar y hacer buen uso de los bienes asignados, contribuyendo a prolongar su vida útil y garantizando su adecuada conservación.", fNorm);
        }
        else
        {
            DrawPara($"Yo, {d.Colaborador}, portador(a) del Documento Único de Identidad número {d.Identificacion}, quien desempeña el cargo de {d.Area}, por este medio hago constar que recibo en calidad de préstamo temporal de Global Customs Solutions S.E.M. de C.V. el siguiente equipo:", fNorm);

            DrawEquipoLista();

            string condicion = string.IsNullOrEmpty(d.Observaciones)
                ? "Me comprometo a devolver el equipo en el mismo estado en que fue recibido, en la fecha acordada o cuando la empresa lo requiera."
                : $"El préstamo tiene carácter temporal bajo las siguientes condiciones: {d.Observaciones}. Me comprometo a devolver el equipo en el mismo estado en que fue recibido.";
            DrawPara(condicion, fNorm);

            DrawPara("Me comprometo a no ceder, prestar o transferir el equipo a terceros sin autorización expresa del Departamento de Tecnología.", fNorm);
            DrawPara("Cualquier daño o pérdida ocurrida durante el período de préstamo será bajo mi responsabilidad.", fNorm);
            DrawPara("Asimismo, manifiesto que tengo conocimiento de las siguientes disposiciones:", fNorm, spaceAfter: 4);

            DrawPara("- Está prohibida la instalación de software, aplicaciones o herramientas no autorizadas por el Departamento de Tecnología.", fNorm, indentL: 8, spaceAfter: 3);
            DrawPara("- No está permitido realizar configuraciones que comprometan la seguridad de la información corporativa.", fNorm, indentL: 8, spaceAfter: 3);
            DrawPara("- En el caso de teléfonos corporativos, cualquier cargo adicional al plan asignado, incluyendo suscripciones, promociones, compras, mensajes premium o consumos no autorizados, será responsabilidad exclusiva del usuario.", fNorm, indentL: 8, spaceAfter: 3);
            DrawPara("- Me comprometo a seguir las políticas de seguridad informática y las buenas prácticas establecidas por la empresa, especialmente en el uso de redes públicas, navegación en internet y manejo de información confidencial.", fNorm, indentL: 8, spaceAfter: 7);

            DrawPara("Finalmente, me comprometo a cuidar y hacer buen uso de los bienes asignados, contribuyendo a prolongar su vida útil y garantizando su adecuada conservación.", fNorm);
        }

        // ── Pie de página ─────────────────────────────────────────────
        g.DrawLine(new XPen(gray, 0.5), ML, H - 30, ML + TW, H - 30);
        var fmtPie = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Near };
        g.DrawString("Global Customs Solutions S.E.M. de C.V.  |  Departamento de Tecnología  |  Documento confidencial",
            new XFont("Arial", 7, XFontStyle.Regular), XBrushes.Gray,
            new XRect(ML, H - 22, TW, 12), fmtPie);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  PLANTILLA: ASIGNACIÓN — Cara 1: carta legal | Cara 2: detalle equipo
    // ─────────────────────────────────────────────────────────────────────
    private byte[] GenerarDocumentoAsignacion(FiniquitoData d)
    {
        var doc = new PdfDocument();
        GenerarCaraFrontal(doc, d, esPrestamo: false);  // Página 1
        GenerarDocumentoDetalle(doc, d, "Asignacion de equipo tecnologico al colaborador.");  // Página 2
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  PLANTILLA: PRÉSTAMO — Cara 1: carta legal | Cara 2: detalle equipo
    // ─────────────────────────────────────────────────────────────────────
    private byte[] GenerarDocumentoPrestamo(FiniquitoData d)
    {
        var doc = new PdfDocument();
        GenerarCaraFrontal(doc, d, esPrestamo: true);   // Página 1
        GenerarDocumentoDetalle(doc, d, "Prestamo temporal de equipo tecnologico al colaborador.");  // Página 2
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  PLANTILLA: FINIQUITO (DEVOLUCIÓN) — página única con tabla
    // ─────────────────────────────────────────────────────────────────────
    private byte[] GenerarDocumentoFiniquito(FiniquitoData d)
        => GenerarDocumento(d);

    // Página de detalle (cara 2 de asignación/préstamo)
    private void GenerarDocumentoDetalle(PdfDocument doc, FiniquitoData d, string? headerText = null)
    {
        var page = doc.AddPage();
        page.Size = PdfSharpCore.PageSize.Letter;
        var g = XGraphics.FromPdfPage(page);
        DibujarPaginaDetalle(g, page, d, headerText);
    }

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
    public string TelNumero      { get; set; } = "";
    public string TelMarca       { get; set; } = "";
    public string TelModelo      { get; set; } = "";
    public string TelImei        { get; set; } = "";
    public string Motivo         { get; set; } = "fin_laboral";
    public string ReceptorNombre { get; set; } = "";
    public string ReceptorCentro { get; set; } = "GCS Santa Elena";
    public string FirmaEmpleadoBase64 { get; set; } = "";
    public string? RutaFirmaIT { get; set; }
    public List<PerifericoFiniquito> Perifericos { get; set; } = [];
}

public class PerifericoFiniquito
{
    public string Tipo        { get; set; } = "";
    public string Marca       { get; set; } = "";
    public string Modelo      { get; set; } = "";
    public string NumeroSerie { get; set; } = "";
}
