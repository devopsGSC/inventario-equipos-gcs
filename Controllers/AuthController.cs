using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using InventarioTI.Models;

namespace InventarioTI.Controllers;

public class AuthController : Controller
{
    private readonly SignInManager<UsuarioApp> _signIn;
    private readonly UserManager<UsuarioApp>  _users;
    public AuthController(SignInManager<UsuarioApp> signIn, UserManager<UsuarioApp> users)
    { _signIn = signIn; _users = users; }

    [AllowAnonymous, HttpGet] public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index","Home");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, bool recuerdame, string? returnUrl)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user == null || !user.Activo)
        {
            ModelState.AddModelError("", "Credenciales incorrectas o usuario inactivo.");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
        var result = await _signIn.PasswordSignInAsync(user, password, recuerdame, lockoutOnFailure: true);
        if (result.Succeeded)
            return LocalRedirect(returnUrl ?? "/");
        if (result.IsLockedOut)
            ModelState.AddModelError("", "Cuenta bloqueada por múltiples intentos fallidos. Intenta en 15 minutos.");
        else
            ModelState.AddModelError("", "Correo o contraseña incorrectos.");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Login");
    }

    [AllowAnonymous]
    public IActionResult AccesoDenegado() => View();

    public async Task<IActionResult> Perfil()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Perfil(string nombreCompleto, string cargo,
        string? passwordActual, string? passwordNueva, IFormFile? firmaIT)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return NotFound();

        user.NombreCompleto = nombreCompleto;
        user.Cargo          = cargo;

        if (!string.IsNullOrWhiteSpace(passwordNueva))
        {
            if (string.IsNullOrWhiteSpace(passwordActual))
            {
                TempData["Error"] = "Debes ingresar tu contraseña actual para cambiarla.";
                return RedirectToAction(nameof(Perfil));
            }
            var cambio = await _users.ChangePasswordAsync(user, passwordActual, passwordNueva);
            if (!cambio.Succeeded)
            {
                TempData["Error"] = string.Join(" ", cambio.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Perfil));
            }
        }

        if (firmaIT != null && firmaIT.Length > 0)
        {
            var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "firmas-it");
            Directory.CreateDirectory(carpeta);
            var ext = Path.GetExtension(firmaIT.FileName);
            var nombreArchivo = $"firma_{user.Id}{ext}";
            var rutaCompleta  = Path.Combine(carpeta, nombreArchivo);
            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                await firmaIT.CopyToAsync(stream);
            user.RutaFirmaIT = $"/uploads/firmas-it/{nombreArchivo}";
        }

        await _users.UpdateAsync(user);
        TempData["OK"] = "Perfil actualizado correctamente.";
        return RedirectToAction(nameof(Perfil));
    }
}
