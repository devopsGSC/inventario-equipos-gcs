using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace InventarioTI.Services;

// Mantiene, por sesión de usuario, la pila de pantallas visitadas para que
// el link "Volver" de cualquier vista regrese a la pantalla anterior exacta
// (con sus filtros, búsqueda y paginación) en lugar de a una ruta fija.
public static class NavegacionHistorial
{
    private const string ClaveSesion = "NavStack";
    private const int Maximo = 30;

    public static void Registrar(ISession sesion, string url)
    {
        var pila = Obtener(sesion);
        if (pila.Count > 0 && pila[^1] == url) return;

        pila.Add(url);
        if (pila.Count > Maximo) pila.RemoveAt(0);
        Guardar(sesion, pila);
    }

    // Descarta la pantalla actual (tope de la pila) y devuelve la anterior,
    // que vuelve a quedar fuera de la pila (se re-agrega al cargarse).
    public static string Volver(ISession sesion, string urlPorDefecto)
    {
        var pila = Obtener(sesion);
        if (pila.Count > 0) pila.RemoveAt(pila.Count - 1);

        var destino = pila.Count > 0 ? pila[^1] : urlPorDefecto;
        if (pila.Count > 0) pila.RemoveAt(pila.Count - 1);

        Guardar(sesion, pila);
        return destino;
    }

    private static List<string> Obtener(ISession sesion)
    {
        var json = sesion.GetString(ClaveSesion);
        if (json is null) return new List<string>();
        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    private static void Guardar(ISession sesion, List<string> pila) =>
        sesion.SetString(ClaveSesion, JsonSerializer.Serialize(pila));
}
