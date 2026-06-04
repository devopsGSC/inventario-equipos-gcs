using Microsoft.EntityFrameworkCore;
using InventarioTI.Models;

namespace InventarioTI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<TipoEquipo> TiposEquipo => Set<TipoEquipo>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();
    public DbSet<TipoPeriferico> TiposPerifericos => Set<TipoPeriferico>();
    public DbSet<Periferico> Perifericos => Set<Periferico>();
    public DbSet<EquipoPeriferico> EquiposPerifericos => Set<EquipoPeriferico>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
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

        mb.Entity<Periferico>()
            .HasIndex(p => p.NumeroSerie).IsUnique();
        mb.Entity<Periferico>()
            .Property(p => p.Estado).HasDefaultValue("Disponible");
        mb.Entity<TipoPeriferico>()
            .ToTable("TiposPeriferico");
        mb.Entity<EquipoPeriferico>()
            .HasOne(ep => ep.Equipo)
            .WithMany(e => e.EquiposPerifericos)
            .HasForeignKey(ep => ep.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);
        mb.Entity<EquipoPeriferico>()
            .HasOne(ep => ep.Periferico)
            .WithMany(p => p.EquiposPerifericos)
            .HasForeignKey(ep => ep.PerifericoId)
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
