using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using InventarioTI.Models;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

[Authorize(Roles = "Administrador")]
public class UsuariosController : Controller
{
    private readonly UserManager<UsuarioApp>  _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly SignInManager<UsuarioApp> _signIn;

    public UsuariosController(UserManager<UsuarioApp> users, RoleManager<IdentityRole> roles, SignInManager<UsuarioApp> signIn)
    { _users = users; _roles = roles; _signIn = signIn; }

    public async Task<IActionResult> Index()
    {
        var usuarios = _users.Users.OrderBy(u => u.NombreCompleto).ToList();
        var vm = new List<UsuarioListItemViewModel>();
        foreach (var u in usuarios)
        {
            var roles = await _users.GetRolesAsync(u);
            vm.Add(new UsuarioListItemViewModel { Usuario = u, Rol = roles.FirstOrDefault() ?? "" });
        }
        return View(vm);
    }

    public IActionResult Create()
    {
        ViewBag.Roles = new[] { "Administrador", "TecnicoIT", "Consulta" };
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string nombreCompleto, string email, string password,
        string cargo, string rol, IFormFile? firmaIT)
    {
        ViewBag.Roles = new[] { "Administrador", "TecnicoIT", "Consulta" };

        if (await _users.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError("", "Ya existe un usuario con ese correo.");
            return View();
        }

        var usuario = new UsuarioApp
        {
            UserName       = email,
            Email          = email,
            NombreCompleto = nombreCompleto,
            Cargo          = cargo,
            Activo         = true,
            EmailConfirmed = true
        };

        var result = await _users.CreateAsync(usuario, password);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors) ModelState.AddModelError("", err.Description);
            return View();
        }

        await _users.AddToRoleAsync(usuario, rol);

        if (firmaIT != null && firmaIT.Length > 0)
        {
            var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "firmas-it");
            Directory.CreateDirectory(carpeta);
            var ext = Path.GetExtension(firmaIT.FileName);
            var nombreArchivo = $"firma_{usuario.Id}{ext}";
            var rutaCompleta  = Path.Combine(carpeta, nombreArchivo);
            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                await firmaIT.CopyToAsync(stream);
            usuario.RutaFirmaIT = $"/uploads/firmas-it/{nombreArchivo}";
            await _users.UpdateAsync(usuario);
        }

        TempData["OK"] = "Usuario creado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var usuario = await _users.FindByIdAsync(id);
        if (usuario == null) return NotFound();
        var roles = await _users.GetRolesAsync(usuario);
        ViewBag.Roles = new[] { "Administrador", "TecnicoIT", "Consulta" };
        ViewBag.RolActual = roles.FirstOrDefault() ?? "";
        return View(usuario);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, string nombreCompleto, string cargo,
        string rol, string? passwordNueva, IFormFile? firmaIT)
    {
        var usuario = await _users.FindByIdAsync(id);
        if (usuario == null) return NotFound();

        usuario.NombreCompleto = nombreCompleto;
        usuario.Cargo          = cargo;

        var rolesActuales = await _users.GetRolesAsync(usuario);
        if (!rolesActuales.Contains(rol))
        {
            if (rolesActuales.Any()) await _users.RemoveFromRolesAsync(usuario, rolesActuales);
            await _users.AddToRoleAsync(usuario, rol);
        }

        if (!string.IsNullOrWhiteSpace(passwordNueva))
        {
            await _users.RemovePasswordAsync(usuario);
            await _users.AddPasswordAsync(usuario, passwordNueva);
        }

        if (firmaIT != null && firmaIT.Length > 0)
        {
            var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "firmas-it");
            Directory.CreateDirectory(carpeta);
            var ext = Path.GetExtension(firmaIT.FileName);
            var nombreArchivo = $"firma_{usuario.Id}{ext}";
            var rutaCompleta  = Path.Combine(carpeta, nombreArchivo);
            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                await firmaIT.CopyToAsync(stream);
            usuario.RutaFirmaIT = $"/uploads/firmas-it/{nombreArchivo}";
        }

        await _users.UpdateAsync(usuario);
        TempData["OK"] = "Usuario actualizado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActivo(string id)
    {
        var usuario = await _users.FindByIdAsync(id);
        if (usuario == null) return NotFound();

        usuario.Activo = !usuario.Activo;
        await _users.UpdateAsync(usuario);

        if (!usuario.Activo && usuario.Id == _users.GetUserId(User))
            await _signIn.SignOutAsync();

        TempData["OK"] = usuario.Activo ? "Usuario activado." : "Usuario desactivado.";
        return RedirectToAction(nameof(Index));
    }
}
