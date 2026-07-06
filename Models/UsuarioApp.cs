using Microsoft.AspNetCore.Identity;

namespace InventarioTI.Models;

public class UsuarioApp : IdentityUser
{
    public string NombreCompleto { get; set; } = "";
    public string Cargo          { get; set; } = "";
    // Ruta al archivo de firma: /uploads/firmas-it/usuario_xxx.png
    public string? RutaFirmaIT   { get; set; }
    public bool    Activo        { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
}
