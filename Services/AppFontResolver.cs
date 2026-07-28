using System.Reflection;
using PdfSharpCore.Fonts;

namespace InventarioTI.Services;

/// <summary>
/// El resolver por defecto de PdfSharpCore necesita fuentes instaladas en el
/// sistema operativo; el servidor de produccion (Linux) no tiene ninguna.
/// Este resolver usa Liberation Sans (compatible en metricas con Arial,
/// licencia SIL OFL) embebida en el ensamblado, sin depender del SO.
/// </summary>
public class AppFontResolver : IFontResolver
{
    public string DefaultFontName => "LiberationSans-Regular";

    public byte[] GetFont(string faceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($"{faceName}.ttf", StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Recurso de fuente no encontrado: {faceName}.ttf");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var faceName = (isBold, isItalic) switch
        {
            (true, true)   => "LiberationSans-BoldItalic",
            (true, false)  => "LiberationSans-Bold",
            (false, true)  => "LiberationSans-Italic",
            (false, false) => "LiberationSans-Regular",
        };
        return new FontResolverInfo(faceName);
    }
}
