using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioTI.Models;

public class Departamento
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Nombre { get; set; } = "";
    public ICollection<Empleado> Empleados { get; set; } = [];
}

public class Empleado
{
    public int Id { get; set; }
    [Required, MaxLength(20), Display(Name = "Código")]
    public string CodigoEmpleado { get; set; } = "";
    [Required, MaxLength(150)]
    public string Nombre { get; set; } = "";
    [Required, MaxLength(20)]
    public string DUI { get; set; } = "";
    [Required, MaxLength(100)]
    public string Cargo { get; set; } = "";
    [Display(Name = "Departamento")]
    public int DepartamentoId { get; set; }
    public bool Activo { get; set; } = true;
    public Departamento? Departamento { get; set; }
    public ICollection<Movimiento> Movimientos { get; set; } = [];
}

public class TipoEquipo
{
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string Nombre { get; set; } = "";
    public ICollection<Equipo> Equipos { get; set; } = [];
}

public class Equipo
{
    public int Id { get; set; }
    [Required, MaxLength(150), Display(Name = "Nombre del equipo")]
    public string NombreEquipo { get; set; } = "";
    [Required, MaxLength(100)]
    public string Marca { get; set; } = "";
    [Required, MaxLength(100)]
    public string Modelo { get; set; } = "";
    [Required, MaxLength(100), Display(Name = "Número de serie")]
    public string NumeroSerie { get; set; } = "";
    [Display(Name = "Tipo")]
    public int TipoEquipoId { get; set; }
    [MaxLength(500)]
    public string? Accesorios { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal? Costo { get; set; }
    [Display(Name = "Fecha de compra")]
    public DateTime? FechaCompra { get; set; }
    [Display(Name = "Garantía hasta")]
    public DateTime? FechaGarantia { get; set; }
    [MaxLength(20)]
    public string Estado { get; set; } = "Bodega";
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    public TipoEquipo? TipoEquipo { get; set; }
    public ICollection<Movimiento> Movimientos { get; set; } = [];
}

public class Movimiento
{
    public int Id { get; set; }
    public int EquipoId { get; set; }
    public int? EmpleadoId { get; set; }
    [Required, MaxLength(20), Display(Name = "Tipo")]
    public string TipoMovimiento { get; set; } = "";
    [Display(Name = "Fecha inicio")]
    public DateTime FechaInicio { get; set; } = DateTime.Now;
    [Display(Name = "Devolución estimada")]
    public DateTime? FechaFinEstimada { get; set; }
    [Display(Name = "Fecha de devolución")]
    public DateTime? FechaDevolucion { get; set; }
    [MaxLength(500)]
    public string? Observaciones { get; set; }
    public bool CartaGenerada { get; set; } = false;
    public string? FirmaEmpleado { get; set; }
    public Equipo? Equipo { get; set; }
    public Empleado? Empleado { get; set; }
}

public class TipoPeriferico
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Nombre { get; set; } = "";
    public ICollection<Periferico> Perifericos { get; set; } = [];
}

public class Periferico
{
    public int Id { get; set; }
    [Display(Name = "Tipo")]
    public int TipoPerifericoId { get; set; }
    [Required, MaxLength(100)]
    public string Marca { get; set; } = "";
    [Required, MaxLength(100)]
    public string Modelo { get; set; } = "";
    [Required, MaxLength(100), Display(Name = "Número de serie")]
    public string NumeroSerie { get; set; } = "";
    [MaxLength(20)]
    public string Estado { get; set; } = "Disponible";
    [MaxLength(500)]
    public string? Observaciones { get; set; }
    [Display(Name = "Fecha de compra")]
    public DateTime? FechaCompra { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    public TipoPeriferico? TipoPeriferico { get; set; }
    public ICollection<EquipoPeriferico> EquiposPerifericos { get; set; } = [];
}

public class EquipoPeriferico
{
    public int Id { get; set; }
    public int EquipoId { get; set; }
    public int PerifericoId { get; set; }
    public DateTime FechaAsignacion { get; set; } = DateTime.Now;
    public DateTime? FechaDesvinculacion { get; set; }
    public Equipo? Equipo { get; set; }
    public Periferico? Periferico { get; set; }
}
