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
    public List<Equipo> UltimosRegistros { get; set; } = [];
    public List<Movimiento> UltimosMovimientos { get; set; } = [];
    public Equipo? ResultadoBusqueda { get; set; }
    public string? SerieBuscada { get; set; }
}

public class MovimientoCreateViewModel
{
    public int EquipoId { get; set; }
    public Equipo? Equipo { get; set; }
    public string? TipoMovimiento { get; set; }
    public int? EmpleadoId { get; set; }
    public DateTime FechaInicio { get; set; } = DateTime.Now;
    public DateTime? FechaFinEstimada { get; set; }
    public string? Observaciones { get; set; }
    public string? FirmaEmpleado { get; set; }
    public int? SitioId { get; set; }
    public List<Empleado> Empleados { get; set; } = [];
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
