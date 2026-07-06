using Microsoft.AspNetCore.Identity;
using InventarioTI.Models;

namespace InventarioTI.Services;

public class SeedService
{
    private readonly UserManager<UsuarioApp>  _users;
    private readonly RoleManager<IdentityRole> _roles;

    public SeedService(UserManager<UsuarioApp> users, RoleManager<IdentityRole> roles)
    { _users = users; _roles = roles; }

    public async Task InicializarAsync()
    {
        // Crear roles si no existen
        string[] roles = ["Administrador", "TecnicoIT", "Consulta"];
        foreach (var rol in roles)
            if (!await _roles.RoleExistsAsync(rol))
                await _roles.CreateAsync(new IdentityRole(rol));

        // Crear usuario admin inicial si no existe ningún admin
        const string adminEmail = "admin@gcs.com.sv";
        if (await _users.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new UsuarioApp
            {
                UserName       = adminEmail,
                Email          = adminEmail,
                NombreCompleto = "Administrador del Sistema",
                Cargo          = "IT Admin",
                Activo         = true,
                EmailConfirmed = true
            };
            var result = await _users.CreateAsync(admin, "Admin2024!");
            if (result.Succeeded)
                await _users.AddToRoleAsync(admin, "Administrador");
        }
    }
}
