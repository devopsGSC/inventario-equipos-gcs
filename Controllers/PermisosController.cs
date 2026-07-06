using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;

namespace InventarioTI.Controllers;

[Authorize(Roles = "Administrador")]
public class PermisosController : Controller
{
    private readonly AppDbContext  _db;
    private readonly PermisoService _permisos;

    public PermisosController(AppDbContext db, PermisoService permisos)
    { _db = db; _permisos = permisos; }

    // Vista principal: matriz de permisos por rol
    public async Task<IActionResult> Index()
    {
        var modulos = await _db.Modulos
            .Include(m => m.Acciones)
            .OrderBy(m => m.Id)
            .ToListAsync();

        var roles = new[] { "Administrador", "TecnicoIT", "Consulta" };

        var permisosActuales = await _db.PermisosRol.ToListAsync();
        var permisosDict = permisosActuales.ToDictionary(
            p => $"{p.RolNombre}::{p.AccionClave}",
            p => p.Permitido);

        ViewBag.Modulos       = modulos;
        ViewBag.Roles         = roles;
        ViewBag.PermisosDict  = permisosDict;
        return View();
    }

    // Guardar cambios de permisos para un rol
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarPermisos(string rolNombre,
        [FromForm] Dictionary<string, bool> permisos)
    {
        // Proteger: el rol Administrador siempre mantiene usuarios.permisos
        if (rolNombre == "Administrador")
        {
            permisos["usuarios.permisos"] = true;
            permisos["usuarios.ver"]      = true;
        }

        var existentes = await _db.PermisosRol
            .Where(p => p.RolNombre == rolNombre)
            .ToListAsync();

        // Actualizar los que ya existen
        foreach (var existente in existentes)
        {
            existente.Permitido = permisos.TryGetValue(existente.AccionClave, out bool val) && val;
        }

        // Agregar los que no existen aún (por si se agregaron nuevos módulos)
        var clavesExistentes = existentes.Select(e => e.AccionClave).ToHashSet();
        foreach (var (clave, permitido) in permisos)
        {
            if (!clavesExistentes.Contains(clave))
                _db.PermisosRol.Add(new PermisoRol
                    { RolNombre = rolNombre, AccionClave = clave, Permitido = permitido });
        }

        await _db.SaveChangesAsync();
        _permisos.InvalidarCache(); // Limpiar caché inmediatamente

        TempData["OK"] = $"Permisos del rol '{rolNombre}' actualizados correctamente.";
        return RedirectToAction(nameof(Index));
    }

    public record TogglePermisoDto(string RolNombre, string AccionClave, bool Permitido);

    // API para toggle individual (para la UI interactiva)
    [HttpPost]
    public async Task<IActionResult> TogglePermiso([FromBody] TogglePermisoDto dto)
    {
        // No permitir quitar permisos críticos del Administrador
        if (dto.RolNombre == "Administrador" &&
            (dto.AccionClave == "usuarios.permisos" || dto.AccionClave == "usuarios.ver"))
            return BadRequest("No se puede modificar este permiso del Administrador.");

        var permiso = await _db.PermisosRol
            .FirstOrDefaultAsync(p => p.RolNombre == dto.RolNombre && p.AccionClave == dto.AccionClave);

        if (permiso == null)
        {
            _db.PermisosRol.Add(new PermisoRol
                { RolNombre = dto.RolNombre, AccionClave = dto.AccionClave, Permitido = dto.Permitido });
        }
        else
        {
            permiso.Permitido = dto.Permitido;
        }

        await _db.SaveChangesAsync();
        _permisos.InvalidarCache();

        return Ok(new { dto.RolNombre, dto.AccionClave, dto.Permitido });
    }
}
