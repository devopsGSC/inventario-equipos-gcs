using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using InventarioTI.Models;

namespace InventarioTI.Data;

public class AppDbContext : IdentityDbContext<UsuarioApp>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<MiembroExterno> MiembrosExternos => Set<MiembroExterno>();
    public DbSet<Grupo> Grupos => Set<Grupo>();
    public DbSet<Sitio> Sitios => Set<Sitio>();
    public DbSet<TipoEquipo> TiposEquipo => Set<TipoEquipo>();
    public DbSet<PlanData> PlanesData => Set<PlanData>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();
    public DbSet<TipoPeriferico> TiposPerifericos => Set<TipoPeriferico>();
    public DbSet<Periferico> Perifericos => Set<Periferico>();
    public DbSet<EquipoPeriferico> EquiposPerifericos => Set<EquipoPeriferico>();
    public DbSet<ImagenMovimiento> ImagenesMovimiento => Set<ImagenMovimiento>();
    public DbSet<AccesorioEquipo> AccesoriosEquipo => Set<AccesorioEquipo>();
    public DbSet<Modulo> Modulos => Set<Modulo>();
    public DbSet<AccionModulo> AccionesModulo => Set<AccionModulo>();
    public DbSet<PermisoRol> PermisosRol => Set<PermisoRol>();
    public DbSet<OperacionMasiva> OperacionesMasivas => Set<OperacionMasiva>();
    public DbSet<DetalleOperacionMasiva> DetalleOperacionMasiva => Set<DetalleOperacionMasiva>();
    public DbSet<TipoLicencia> TiposLicencia => Set<TipoLicencia>();
    public DbSet<LicenciaAsignacion> LicenciasAsignaciones => Set<LicenciaAsignacion>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<Empleado>()
            .HasIndex(e => e.CodigoEmpleado).IsUnique();
        mb.Entity<Empleado>()
            .HasIndex(e => e.DUI).IsUnique();
        mb.Entity<Equipo>()
            .HasIndex(e => e.NumeroSerie).IsUnique();
        mb.Entity<Equipo>()
            .Property(e => e.Estado)
            .HasDefaultValue("Bodega");
        mb.Entity<Movimiento>()
            .HasOne(m => m.Empleado)
            .WithMany(e => e.Movimientos)
            .HasForeignKey(m => m.EmpleadoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Movimiento>()
            .HasOne(m => m.Equipo)
            .WithMany(e => e.Movimientos)
            .HasForeignKey(m => m.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Movimiento>()
            .HasOne(m => m.Sitio)
            .WithMany()
            .HasForeignKey(m => m.SitioId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Movimiento>()
            .HasOne(m => m.MiembroExterno)
            .WithMany(x => x.Movimientos)
            .HasForeignKey(m => m.MiembroExternoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Movimiento>()
            .HasOne(m => m.Grupo)
            .WithMany(x => x.Movimientos)
            .HasForeignKey(m => m.GrupoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Equipo>()
            .HasOne(e => e.PlanData)
            .WithMany()
            .HasForeignKey(e => e.PlanDataId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
        mb.Entity<ImagenMovimiento>()
            .HasOne(i => i.Movimiento)
            .WithMany(m => m.Imagenes)
            .HasForeignKey(i => i.MovimientoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ImagenMovimiento>()
            .HasOne(i => i.EquipoPeriferico)
            .WithMany(ep => ep.Imagenes)
            .HasForeignKey(i => i.EquipoPerifericoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Periferico>()
            .HasIndex(p => p.NumeroSerie).IsUnique();
        mb.Entity<Periferico>()
            .Property(p => p.Estado).HasDefaultValue("Disponible");
        mb.Entity<TipoPeriferico>()
            .ToTable("TiposPeriferico");
        mb.Entity<Modulo>().ToTable("Modulos");
        mb.Entity<AccionModulo>()
            .ToTable("AccionesModulo")
            .HasOne(a => a.Modulo)
            .WithMany(m => m.Acciones)
            .HasForeignKey(a => a.ModuloId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<PermisoRol>()
            .ToTable("PermisosRol")
            .HasIndex(p => new { p.RolNombre, p.AccionClave }).IsUnique();
        mb.Entity<DetalleOperacionMasiva>()
            .HasOne(d => d.Operacion)
            .WithMany(o => o.Detalles)
            .HasForeignKey(d => d.OperacionId)
            .OnDelete(DeleteBehavior.Cascade);
        mb.Entity<AccesorioEquipo>()
            .ToTable("AccesoriosEquipo")
            .HasIndex(a => a.Nombre).IsUnique();
        mb.Entity<AccesorioEquipo>().HasData(
            new AccesorioEquipo { Id = 1, Nombre = "Cargador" },
            new AccesorioEquipo { Id = 2, Nombre = "Funda" }
        );
        mb.Entity<EquipoPeriferico>()
            .HasOne(ep => ep.Equipo)
            .WithMany(e => e.EquiposPerifericos)
            .HasForeignKey(ep => ep.EquipoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<EquipoPeriferico>()
            .HasOne(ep => ep.Periferico)
            .WithMany(p => p.EquiposPerifericos)
            .HasForeignKey(ep => ep.PerifericoId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<EquipoPeriferico>()
            .HasOne(ep => ep.Empleado)
            .WithMany()
            .HasForeignKey(ep => ep.EmpleadoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<EquipoPeriferico>()
            .HasOne(ep => ep.Sitio)
            .WithMany()
            .HasForeignKey(ep => ep.SitioId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<EquipoPeriferico>()
            .HasOne(ep => ep.MiembroExterno)
            .WithMany()
            .HasForeignKey(ep => ep.MiembroExternoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<EquipoPeriferico>()
            .HasOne(ep => ep.Grupo)
            .WithMany()
            .HasForeignKey(ep => ep.GrupoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Grupo>()
            .HasIndex(g => g.Nombre).IsUnique();

        mb.Entity<LicenciaAsignacion>()
            .HasOne(la => la.TipoLicencia)
            .WithMany(t => t.Asignaciones)
            .HasForeignKey(la => la.TipoLicenciaId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<LicenciaAsignacion>()
            .HasOne(la => la.Equipo)
            .WithMany()
            .HasForeignKey(la => la.EquipoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<LicenciaAsignacion>()
            .HasOne(la => la.Empleado)
            .WithMany()
            .HasForeignKey(la => la.EmpleadoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<LicenciaAsignacion>()
            .HasOne(la => la.MiembroExterno)
            .WithMany()
            .HasForeignKey(la => la.MiembroExternoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<LicenciaAsignacion>()
            .HasOne(la => la.Grupo)
            .WithMany()
            .HasForeignKey(la => la.GrupoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<TipoPeriferico>().HasData(
            new TipoPeriferico { Id = 1, Nombre = "Monitor" },
            new TipoPeriferico { Id = 2, Nombre = "Headset" },
            new TipoPeriferico { Id = 3, Nombre = "Mochila HP" },
            new TipoPeriferico { Id = 4, Nombre = "Teclado y Ratón" },
            new TipoPeriferico { Id = 5, Nombre = "Otro" }
        );

        // Seed
        mb.Entity<Departamento>().HasData(
            new Departamento { Id = 1, Nombre = "Tecnología de la Información" },
            new Departamento { Id = 2, Nombre = "Recursos Humanos" },
            new Departamento { Id = 3, Nombre = "Finanzas" },
            new Departamento { Id = 4, Nombre = "Operaciones" },
            new Departamento { Id = 5, Nombre = "Gerencia General" },
            new Departamento { Id = 6, Nombre = "Comercial" }
        );
        mb.Entity<TipoEquipo>().HasData(
            new TipoEquipo { Id = 1, Nombre = "Laptop" },
            new TipoEquipo { Id = 2, Nombre = "Teléfono" },
            new TipoEquipo { Id = 3, Nombre = "Tablet" },
            new TipoEquipo { Id = 4, Nombre = "Desktop" },
            new TipoEquipo { Id = 5, Nombre = "Monitor" },
            new TipoEquipo { Id = 6, Nombre = "Impresora" }
        );
    }
}
