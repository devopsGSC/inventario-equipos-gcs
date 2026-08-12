using InventarioTI.Models;

namespace InventarioTI.ViewModels;

public class DashboardViewModel
{
    public int TotalEquipos { get; set; }
    public int EnBodega { get; set; }
    public int Asignados { get; set; }
    public int EnPrestamo { get; set; }
    public int EnGarantia { get; set; }
    public int EnBaja { get; set; }
    public int GarantiasProximas { get; set; }
    public int GarantiasVencidas { get; set; }
    public List<ActividadItem> ActividadReciente { get; set; } = [];
    public Equipo? ResultadoBusqueda { get; set; }
    public string? SerieBuscada { get; set; }
}

// Un renglón del feed "Actividad reciente" del panel general: une
// movimientos, registros y asignaciones de equipos/periféricos/licencias/
// personas en una sola línea de tiempo, con quién hizo la acción (cuando
// se conoce — solo se registra a partir de que existe este campo).
public class ActividadItem
{
    public DateTime Fecha { get; set; }
    public string Icono { get; set; } = "bi-circle";
    public string BadgeClase { get; set; } = "badge-bodega";
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string? Responsable { get; set; }
    public string? Usuario { get; set; }
    public string? LinkController { get; set; }
    public string? LinkAction { get; set; }
    public int? LinkId { get; set; }
}

// Un renglón de la pantalla central "Cartas": une en una sola lista las
// cartas de asignación/préstamo de equipo, las de periféricos asignados
// "Directo" y las cartas generales, cada una con su estado de entrega y
// sus links de acción, para no tener que ir a buscarlas por separado en
// el detalle de cada equipo/empleado/periférico.
public class CartaListItem
{
    public string Tipo { get; set; } = ""; // "Equipo" | "Periferico" | "General"
    public string Responsable { get; set; } = "";
    public string Detalle { get; set; } = "";
    public DateTime Fecha { get; set; }
    public bool EntregaCompletada { get; set; }
    public string? EntregadoPor { get; set; }
    public DateTime? FechaEntrega { get; set; }
    public bool TieneFirma { get; set; }
    public string LinkVer { get; set; } = "";
    public string LinkDescargar { get; set; } = "";
    public string? LinkFirmaRemota { get; set; }
}

public class MovimientoCreateViewModel
{
    public int EquipoId { get; set; }
    public Equipo? Equipo { get; set; }
    public string? TipoMovimiento { get; set; }
    public string TipoResponsable { get; set; } = "Empleado"; // "Empleado" | "MiembroExterno" | "Grupo"
    public int? EmpleadoId { get; set; }
    public int? MiembroExternoId { get; set; }
    public int? GrupoId { get; set; }
    public DateTime FechaInicio { get; set; } = DateTime.Now;
    public DateTime? FechaFinEstimada { get; set; }
    public string? Observaciones { get; set; }
    public string? FirmaEmpleado { get; set; }
    public int? SitioId { get; set; }
    public List<Empleado> Empleados { get; set; } = [];
    public List<MiembroExterno> MiembrosExternos { get; set; } = [];
    public List<Grupo> Grupos { get; set; } = [];
    public List<Sitio> Sitios { get; set; } = [];
}

public class EquipoDetalleViewModel
{
    public Equipo Equipo { get; set; } = null!;
    public List<Movimiento> Historial { get; set; } = [];
    public Movimiento? MovimientoActivo { get; set; }
}

public class UsuarioListItemViewModel
{
    public UsuarioApp Usuario { get; set; } = null!;
    public string Rol { get; set; } = "";
}

public class ResetPasswordViewModel
{
    public string UserId { get; set; } = "";
    public string Token { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}
