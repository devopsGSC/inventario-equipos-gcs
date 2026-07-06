using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;

namespace InventarioTI.Services;

public class PermisoService
{
    private readonly AppDbContext  _db;
    private readonly IMemoryCache  _cache;
    private const string CacheKey = "permisos_roles";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public PermisoService(AppDbContext db, IMemoryCache cache)
    { _db = db; _cache = cache; }

    // Verifica si un rol tiene un permiso específico
    public async Task<bool> TienePermiso(string rolNombre, string accionClave)
    {
        var permisos = await ObtenerPermisos();
        var key = $"{rolNombre}::{accionClave}";
        return permisos.TryGetValue(key, out bool permitido) && permitido;
    }

    // Verifica si el usuario (que puede tener varios roles) tiene el permiso
    public async Task<bool> UsuarioTienePermiso(IEnumerable<string> roles, string accionClave)
    {
        foreach (var rol in roles)
            if (await TienePermiso(rol, accionClave)) return true;
        return false;
    }

    // Conveniencia: verifica el permiso directamente desde el ClaimsPrincipal del usuario logueado
    public async Task<bool> TienePermiso(ClaimsPrincipal user, string accionClave)
    {
        var roles = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);
        return await UsuarioTienePermiso(roles, accionClave);
    }

    // Obtener todos los permisos (con caché)
    private async Task<Dictionary<string, bool>> ObtenerPermisos()
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, bool>? cached) && cached != null)
            return cached;

        var permisos = await _db.PermisosRol.ToListAsync();
        var dict = permisos.ToDictionary(
            p => $"{p.RolNombre}::{p.AccionClave}",
            p => p.Permitido);

        _cache.Set(CacheKey, dict, CacheDuration);
        return dict;
    }

    // Invalidar caché cuando se modifiquen permisos
    public void InvalidarCache() => _cache.Remove(CacheKey);

    // Obtener todos los permisos de un rol para la UI
    public async Task<List<PermisoRol>> ObtenerPermisosDeRol(string rolNombre) =>
        await _db.PermisosRol.Where(p => p.RolNombre == rolNombre).ToListAsync();
}
