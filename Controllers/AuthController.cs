using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using InventarioTI.Models;
using InventarioTI.Services;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class AuthController : Controller
{
    private readonly SignInManager<UsuarioApp> _signIn;
    private readonly UserManager<UsuarioApp>  _users;
    private readonly EmailService _email;
    private readonly PermisoService _permisos;
    public AuthController(SignInManager<UsuarioApp> signIn, UserManager<UsuarioApp> users, EmailService email, PermisoService permisos)
    { _signIn = signIn; _users = users; _email = email; _permisos = permisos; }

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

    [AllowAnonymous, HttpGet]
    public IActionResult ForgotPassword() => View();

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user != null && user.Activo)
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action(nameof(ResetPassword), "Auth",
                new { userId = user.Id, token }, protocol: Request.Scheme);

            var cuerpo = EmailService.PlantillaBoton(
                $"Hola {user.NombreCompleto},",
                "Recibimos una solicitud para restablecer tu contraseña en InventarioTI. " +
                "Haz clic en el botón de abajo para crear una nueva. Si tú no solicitaste este cambio, puedes ignorar este correo.",
                callbackUrl!, "Crear nueva contraseña");

            await _email.EnviarAsync(user.Email!, "Restablecer contraseña — InventarioTI", cuerpo, incluirLogo: true);
        }
        // Se muestra siempre la misma confirmación, exista o no el correo,
        // para no revelar si una cuenta está registrada en el sistema.
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [AllowAnonymous]
    public IActionResult ForgotPasswordConfirmation() => View();

    [AllowAnonymous, HttpGet]
    public IActionResult ResetPassword(string? userId, string? token)
    {
        if (userId == null || token == null) return RedirectToAction(nameof(Login));
        return View(new ResetPasswordViewModel { UserId = userId, Token = token });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError("", "Las contraseñas no coinciden.");
            return View(model);
        }

        var user = await _users.FindByIdAsync(model.UserId);
        if (user == null) return RedirectToAction(nameof(ResetPasswordConfirmation));

        var result = await _users.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors) ModelState.AddModelError("", err.Description);
            return View(model);
        }
        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [AllowAnonymous]
    public IActionResult ResetPasswordConfirmation() => View();

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
        if (await _permisos.TienePermiso(User, "usuarios.editar"))
            user.Cargo = cargo;

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
