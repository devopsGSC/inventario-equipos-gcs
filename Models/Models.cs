using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioTI.Models;

public class Departamento
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre del departamento es obligatorio."), MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Nombre { get; set; } = "";
    public ICollection<Empleado> Empleados { get; set; } = [];
}

public class Empleado
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El código de empleado es obligatorio."), MaxLength(20, ErrorMessage = "Máximo 20 caracteres."), Display(Name = "Código")]
    public string CodigoEmpleado { get; set; } = "";
    [Required(ErrorMessage = "El nombre es obligatorio."), MaxLength(150, ErrorMessage = "Máximo 150 caracteres.")]
    public string Nombre { get; set; } = "";
    [Required(ErrorMessage = "El DUI es obligatorio."), MaxLength(20, ErrorMessage = "Máximo 20 caracteres.")]
    public string DUI { get; set; } = "";
    [Required(ErrorMessage = "El cargo es obligatorio."), MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Cargo { get; set; } = "";
    [Display(Name = "Departamento")]
    public int DepartamentoId { get; set; }
    public bool Activo { get; set; } = true;
    public Departamento? Departamento { get; set; }
    public ICollection<Movimiento> Movimientos { get; set; } = [];
}

public class Sitio
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre del sitio es obligatorio."), MaxLength(150, ErrorMessage = "Máximo 150 caracteres.")]
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; } = true;
}

public class MiembroExterno
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre es obligatorio."), MaxLength(150, ErrorMessage = "Máximo 150 caracteres.")]
    public string Nombre { get; set; } = "";
    [MaxLength(150, ErrorMessage = "Máximo 150 caracteres.")]
    public string? Organizacion { get; set; }
    [MaxLength(20, ErrorMessage = "Máximo 20 caracteres.")]
    public string? Identificacion { get; set; }
    [MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string? Referencia { get; set; }
    public bool Activo { get; set; } = true;
    public ICollection<Movimiento> Movimientos { get; set; } = [];
}

public class Grupo
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre del grupo es obligatorio."), MaxLength(150, ErrorMessage = "Máximo 150 caracteres.")]
    public string Nombre { get; set; } = "";
    [MaxLength(150, ErrorMessage = "Máximo 150 caracteres.")]
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public ICollection<Movimiento> Movimientos { get; set; } = [];
}

public class TipoEquipo
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre del tipo de equipo es obligatorio."), MaxLength(50, ErrorMessage = "Máximo 50 caracteres.")]
    public string Nombre { get; set; } = "";
    public ICollection<Equipo> Equipos { get; set; } = [];
}

public class PlanData
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre del plan es obligatorio."), MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Nombre { get; set; } = "";
}

public class Equipo
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre del equipo es obligatorio."), MaxLength(150, ErrorMessage = "Máximo 150 caracteres."), Display(Name = "Nombre del equipo")]
    public string NombreEquipo { get; set; } = "";
    [Required(ErrorMessage = "La marca es obligatoria."), MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Marca { get; set; } = "";
    [Required(ErrorMessage = "El modelo es obligatorio."), MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Modelo { get; set; } = "";
    [Required(ErrorMessage = "El número de serie es obligatorio."), MaxLength(100, ErrorMessage = "Máximo 100 caracteres."), Display(Name = "Número de serie")]
    public string NumeroSerie { get; set; } = "";
    [Display(Name = "Tipo")]
    public int TipoEquipoId { get; set; }
    [MaxLength(500, ErrorMessage = "Máximo 500 caracteres.")]
    public string? Accesorios { get; set; }
    [MaxLength(50, ErrorMessage = "Máximo 50 caracteres.")]
    public string? IMEI { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal? Costo { get; set; }
    [Display(Name = "Fecha de compra")]
    public DateTime? FechaCompra { get; set; }
    [Display(Name = "Garantía hasta")]
    public DateTime? FechaGarantia { get; set; }
    [MaxLength(20, ErrorMessage = "Máximo 20 caracteres.")]
    public string Estado { get; set; } = "Bodega";
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    [MaxLength(50, ErrorMessage = "Máximo 50 caracteres."), Display(Name = "Memoria RAM")]
    public string? RAM { get; set; }
    [MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string? Procesador { get; set; }
    [MaxLength(50, ErrorMessage = "Máximo 50 caracteres.")]
    public string? Almacenamiento { get; set; }

    // Solo para teléfonos
    [Display(Name = "Plan de datos")]
    public int? PlanDataId { get; set; }
    public PlanData? PlanData { get; set; }

    public TipoEquipo? TipoEquipo { get; set; }
    public ICollection<EquipoPeriferico> EquiposPerifericos { get; set; } = [];
    public ICollection<Movimiento> Movimientos { get; set; } = [];
}

public class Movimiento
{
    public int Id { get; set; }
    public int EquipoId { get; set; }
    public int? EmpleadoId { get; set; }
    public int? MiembroExternoId { get; set; }
    public int? GrupoId { get; set; }
    [Required(ErrorMessage = "El tipo de movimiento es obligatorio."), MaxLength(20, ErrorMessage = "Máximo 20 caracteres."), Display(Name = "Tipo")]
    public string TipoMovimiento { get; set; } = "";
    [Display(Name = "Fecha inicio")]
    public DateTime FechaInicio { get; set; } = DateTime.Now;
    [Display(Name = "Devolución estimada")]
    public DateTime? FechaFinEstimada { get; set; }
    [Display(Name = "Fecha de devolución")]
    public DateTime? FechaDevolucion { get; set; }
    [MaxLength(500, ErrorMessage = "Máximo 500 caracteres.")]
    public string? Observaciones { get; set; }
    public bool CartaGenerada { get; set; } = false;
    public string? FirmaEmpleado { get; set; }
    [Display(Name = "Sitio / Ubicación")]
    public int? SitioId { get; set; }
    public Equipo? Equipo { get; set; }
    public Empleado? Empleado { get; set; }
    public MiembroExterno? MiembroExterno { get; set; }
    public Grupo? Grupo { get; set; }
    public Sitio? Sitio { get; set; }
    public ICollection<ImagenMovimiento> Imagenes { get; set; } = [];

    [NotMapped]
    public string TipoResponsable =>
        Empleado != null ? "Empleado" : MiembroExterno != null ? "MiembroExterno" : Grupo != null ? "Grupo" : "";
    [NotMapped]
    public string NombreResponsable => Empleado?.Nombre ?? MiembroExterno?.Nombre ?? Grupo?.Nombre ?? "—";
}

public class ImagenMovimiento
{
    public int Id { get; set; }
    public int? MovimientoId { get; set; }
    public int? EquipoPerifericoId { get; set; }
    [Required]
    public string RutaImagen { get; set; } = "";
    [MaxLength(200, ErrorMessage = "Máximo 200 caracteres.")]
    public string? Descripcion { get; set; }
    public int Orden { get; set; } = 1;
    public DateTime FechaSubida { get; set; } = DateTime.Now;
    public Movimiento? Movimiento { get; set; }
    public EquipoPeriferico? EquipoPeriferico { get; set; }
}

public class AccesorioEquipo
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre del accesorio es obligatorio."), MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Nombre { get; set; } = "";
}

public class TipoPeriferico
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre del tipo de periférico es obligatorio."), MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Nombre { get; set; } = "";
    public ICollection<Periferico> Perifericos { get; set; } = [];
}

public class Periferico
{
    public int Id { get; set; }
    [Display(Name = "Tipo")]
    public int TipoPerifericoId { get; set; }
    [Required(ErrorMessage = "La marca es obligatoria."), MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Marca { get; set; } = "";
    [Required(ErrorMessage = "El modelo es obligatorio."), MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Modelo { get; set; } = "";
    [Required(ErrorMessage = "El número de serie es obligatorio."), MaxLength(100, ErrorMessage = "Máximo 100 caracteres."), Display(Name = "Número de serie")]
    public string NumeroSerie { get; set; } = "";
    [MaxLength(20, ErrorMessage = "Máximo 20 caracteres.")]
    public string Estado { get; set; } = "Disponible";
    [MaxLength(500, ErrorMessage = "Máximo 500 caracteres.")]
    public string? Observaciones { get; set; }
    [Display(Name = "Fecha de compra")]
    public DateTime? FechaCompra { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    public TipoPeriferico? TipoPeriferico { get; set; }
    public ICollection<EquipoPeriferico> EquiposPerifericos { get; set; } = [];
}

public class Modulo
{
    public int    Id     { get; set; }
    public string Nombre { get; set; } = "";
    public string Icono  { get; set; } = "bi-grid";
    public ICollection<AccionModulo> Acciones { get; set; } = [];
}

public class AccionModulo
{
    public int    Id          { get; set; }
    public int    ModuloId    { get; set; }
    public string Clave       { get; set; } = "";
    public string Nombre      { get; set; } = "";
    public string? Descripcion { get; set; }
    public Modulo? Modulo     { get; set; }
}

public class PermisoRol
{
    public int    Id          { get; set; }
    public string RolNombre   { get; set; } = "";
    public string AccionClave { get; set; } = "";
    public bool   Permitido   { get; set; } = true;
}

public class OperacionMasiva
{
    public int      Id              { get; set; }
    public string   TipoOperacion   { get; set; } = "";
    public DateTime FechaOperacion  { get; set; } = DateTime.Now;
    public string   UsuarioNombre   { get; set; } = "";
    public string   NombreArchivo   { get; set; } = "";
    public int      TotalProcesados { get; set; }
    public int      TotalExitosos   { get; set; }
    public int      TotalOmitidos   { get; set; }
    public int      TotalErrores    { get; set; }
    public string?  Observaciones   { get; set; }
    public ICollection<DetalleOperacionMasiva> Detalles { get; set; } = [];
}

public class DetalleOperacionMasiva
{
    public int     Id                { get; set; }
    public int     OperacionId       { get; set; }
    public int     FilaExcel         { get; set; }
    public string  NumeroSerie       { get; set; } = "";
    public string? NombreEquipo      { get; set; }
    public string  Estado            { get; set; } = "";
    public string? Mensaje           { get; set; }
    public string? CamposModificados { get; set; }
    public OperacionMasiva? Operacion { get; set; }
}

public class EquipoPeriferico
{
    public int Id { get; set; }
    public int? EquipoId { get; set; }
    public int PerifericoId { get; set; }
    public int? EmpleadoId { get; set; }
    public int? MiembroExternoId { get; set; }
    public int? GrupoId { get; set; }
    public string TipoAsignacion { get; set; } = "Equipo"; // "Equipo" | "Directo"
    public string TipoMovimiento { get; set; } = "Asignacion"; // "Asignacion" | "Prestamo" | "Devolucion"
    public DateTime FechaAsignacion { get; set; } = DateTime.Now;
    public DateTime? FechaDesvinculacion { get; set; }
    public DateTime? FechaDevolucionEstimada { get; set; }
    public string? Observaciones { get; set; }
    public string? FirmaEmpleado { get; set; }
    [Display(Name = "Sitio / Ubicación")]
    public int? SitioId { get; set; }
    public Equipo? Equipo { get; set; }
    public Periferico? Periferico { get; set; }
    public Empleado? Empleado { get; set; }
    public MiembroExterno? MiembroExterno { get; set; }
    public Grupo? Grupo { get; set; }
    public Sitio? Sitio { get; set; }
    public ICollection<ImagenMovimiento> Imagenes { get; set; } = [];

    [NotMapped]
    public string TipoResponsable =>
        Empleado != null ? "Empleado" : MiembroExterno != null ? "MiembroExterno" : Grupo != null ? "Grupo" : "";
    [NotMapped]
    public string NombreResponsable => Empleado?.Nombre ?? MiembroExterno?.Nombre ?? Grupo?.Nombre ?? "—";
}
