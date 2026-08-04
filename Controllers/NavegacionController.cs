using Microsoft.AspNetCore.Mvc;
using InventarioTI.Filters;
using InventarioTI.Services;

namespace InventarioTI.Controllers;

public class NavegacionController : Controller
{
    // Destino genérico de los links "Volver" de toda la app: descarta la
    // pantalla actual de la pila de sesión y redirige a la anterior.
    [NoTrackNavigation, HttpGet]
    public IActionResult Volver()
    {
        var destino = NavegacionHistorial.Volver(HttpContext.Session, "/");
        return LocalRedirect(destino);
    }
}
