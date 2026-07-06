using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using InventarioTI.Services;

namespace InventarioTI.Controllers;

public class BaseController : Controller
{
    protected readonly PermisoService Permisos;
    protected BaseController(PermisoService permisos) => Permisos = permisos;

    protected IEnumerable<string> RolesUsuario =>
        User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);

    // Verifica si el usuario actual tiene el permiso indicado
    protected async Task<bool> Puede(string accionClave) =>
        await Permisos.UsuarioTienePermiso(RolesUsuario, accionClave);

    // Verifica si el usuario actual tiene al menos uno de los permisos indicados
    protected async Task<bool> PuedeAlguno(params string[] accionesClave)
    {
        foreach (var clave in accionesClave)
            if (await Puede(clave)) return true;
        return false;
    }

    protected IActionResult AccesoDenegado() => RedirectToAction("AccesoDenegado", "Auth");
}
