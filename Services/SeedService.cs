using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;

namespace InventarioTI.Services;

public class SeedService
{
    private readonly UserManager<UsuarioApp>  _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly AppDbContext _db;

    public SeedService(UserManager<UsuarioApp> users, RoleManager<IdentityRole> roles, AppDbContext db)
    { _users = users; _roles = roles; _db = db; }

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

        // Módulos, acciones y permisos
        await InicializarModulosAsync();
        await InicializarPermisosAsync();
    }

    private async Task InicializarModulosAsync()
    {
        if (await _db.Modulos.AnyAsync()) return;

        var modulos = new List<Modulo>
        {
            new() { Nombre = "Equipos",      Icono = "bi-laptop" },
            new() { Nombre = "Periféricos",  Icono = "bi-plug" },
            new() { Nombre = "Empleados",    Icono = "bi-people" },
            new() { Nombre = "Movimientos",  Icono = "bi-arrow-left-right" },
            new() { Nombre = "Reportes",     Icono = "bi-bar-chart" },
            new() { Nombre = "Carga Masiva", Icono = "bi-file-earmark-arrow-up" },
            new() { Nombre = "Usuarios",     Icono = "bi-person-gear" },
        };
        _db.Modulos.AddRange(modulos);
        await _db.SaveChangesAsync();

        var idPorNombre = modulos.ToDictionary(m => m.Nombre, m => m.Id);
        int equipos = idPorNombre["Equipos"], perifericos = idPorNombre["Periféricos"],
            empleados = idPorNombre["Empleados"], movimientos = idPorNombre["Movimientos"],
            reportes = idPorNombre["Reportes"], cargaMasiva = idPorNombre["Carga Masiva"],
            usuarios = idPorNombre["Usuarios"];

        var acciones = new List<AccionModulo>
        {
            // Equipos
            new() { ModuloId=equipos, Clave="equipos.ver",              Nombre="Ver listado de equipos" },
            new() { ModuloId=equipos, Clave="equipos.detalle",          Nombre="Ver detalle de equipo" },
            new() { ModuloId=equipos, Clave="equipos.crear",            Nombre="Registrar nuevo equipo" },
            new() { ModuloId=equipos, Clave="equipos.editar",           Nombre="Editar equipo" },
            new() { ModuloId=equipos, Clave="equipos.baja",             Nombre="Dar de baja / reactivar" },
            new() { ModuloId=equipos, Clave="equipos.cargamasiva",      Nombre="Carga masiva de equipos" },
            new() { ModuloId=equipos, Clave="equipos.tipos.crear",      Nombre="Crear tipo de equipo personalizado" },
            new() { ModuloId=equipos, Clave="equipos.tipos.eliminar",   Nombre="Eliminar tipo de equipo personalizado" },
            new() { ModuloId=equipos, Clave="equipos.sitio.asignar",    Nombre="Asignar sitio a equipo" },
            new() { ModuloId=equipos, Clave="equipos.planes.crear",     Nombre="Crear plan de datos personalizado" },
            new() { ModuloId=equipos, Clave="equipos.planes.eliminar",  Nombre="Eliminar plan de datos personalizado" },
            // Periféricos
            new() { ModuloId=perifericos, Clave="perifericos.ver",              Nombre="Ver listado de periféricos" },
            new() { ModuloId=perifericos, Clave="perifericos.detalle",          Nombre="Ver detalle de periférico" },
            new() { ModuloId=perifericos, Clave="perifericos.crear",            Nombre="Registrar periférico" },
            new() { ModuloId=perifericos, Clave="perifericos.editar",           Nombre="Editar periférico" },
            new() { ModuloId=perifericos, Clave="perifericos.baja",             Nombre="Dar de baja / reactivar" },
            new() { ModuloId=perifericos, Clave="perifericos.asignar",          Nombre="Asignar periférico a empleado" },
            new() { ModuloId=perifericos, Clave="perifericos.tipos.crear",      Nombre="Crear tipo de periférico personalizado" },
            new() { ModuloId=perifericos, Clave="perifericos.tipos.eliminar",   Nombre="Eliminar tipo de periférico personalizado" },
            new() { ModuloId=perifericos, Clave="perifericos.sitio.asignar",    Nombre="Asignar sitio a periférico" },
            // Empleados
            new() { ModuloId=empleados, Clave="empleados.ver",     Nombre="Ver listado de empleados" },
            new() { ModuloId=empleados, Clave="empleados.detalle", Nombre="Ver detalle de empleado" },
            new() { ModuloId=empleados, Clave="empleados.crear",   Nombre="Registrar empleado" },
            new() { ModuloId=empleados, Clave="empleados.editar",  Nombre="Editar empleado" },
            // Movimientos
            new() { ModuloId=movimientos, Clave="movimientos.ver",         Nombre="Ver historial de movimientos" },
            new() { ModuloId=movimientos, Clave="movimientos.asignar",     Nombre="Registrar asignación" },
            new() { ModuloId=movimientos, Clave="movimientos.prestamo",    Nombre="Registrar préstamo" },
            new() { ModuloId=movimientos, Clave="movimientos.devolucion",  Nombre="Registrar devolución" },
            new() { ModuloId=movimientos, Clave="movimientos.garantia",    Nombre="Registrar entrada/salida de garantía" },
            new() { ModuloId=movimientos, Clave="movimientos.carta",       Nombre="Descargar cartas PDF" },
            new() { ModuloId=movimientos, Clave="movimientos.finiquito",   Nombre="Generar finiquito" },
            new() { ModuloId=movimientos, Clave="movimientos.sitio",       Nombre="Seleccionar sitio al registrar movimiento" },
            new() { ModuloId=movimientos, Clave="sitios.crear",            Nombre="Crear sitio personalizado" },
            new() { ModuloId=movimientos, Clave="sitios.eliminar",         Nombre="Eliminar sitio personalizado" },
            // Reportes
            new() { ModuloId=reportes, Clave="reportes.ver",      Nombre="Ver reportes" },
            new() { ModuloId=reportes, Clave="reportes.exportar", Nombre="Exportar PDF / Excel / CSV" },
            // Carga Masiva
            new() { ModuloId=cargaMasiva, Clave="cargamasiva.usar",         Nombre="Usar carga masiva" },
            new() { ModuloId=cargaMasiva, Clave="actualizacion.masiva",     Nombre="Actualización masiva de equipos" },
            new() { ModuloId=cargaMasiva, Clave="historial.masivo.ver",     Nombre="Ver historial de operaciones masivas" },
            // Usuarios
            new() { ModuloId=usuarios, Clave="usuarios.ver",      Nombre="Ver usuarios" },
            new() { ModuloId=usuarios, Clave="usuarios.crear",    Nombre="Crear usuario" },
            new() { ModuloId=usuarios, Clave="usuarios.editar",   Nombre="Editar usuario" },
            new() { ModuloId=usuarios, Clave="usuarios.permisos", Nombre="Gestionar permisos de roles" },
        };
        _db.AccionesModulo.AddRange(acciones);
        await _db.SaveChangesAsync();
    }

    private async Task InicializarPermisosAsync()
    {
        // Si ya hay permisos para Administrador no hacer nada
        if (await _db.PermisosRol.AnyAsync(p => p.RolNombre == "Administrador"))
            return;

        var todasLasAcciones = await _db.AccionesModulo.Select(a => a.Clave).ToListAsync();

        if (!todasLasAcciones.Any())
            return; // AccionesModulo vacía, el seed de módulos no se ejecutó aún

        var permisos = new List<PermisoRol>();

        foreach (var clave in todasLasAcciones)
        {
            // Administrador: todo permitido
            permisos.Add(new PermisoRol { RolNombre = "Administrador", AccionClave = clave, Permitido = true });

            // TecnicoIT: todo excepto gestión de usuarios, permisos y eliminaciones sensibles
            bool tecnicoPermitido = clave switch
            {
                "usuarios.ver"               => false,
                "usuarios.crear"             => false,
                "usuarios.editar"            => false,
                "usuarios.permisos"          => false,
                "sitios.eliminar"            => false,
                "equipos.planes.eliminar"    => false,
                "equipos.tipos.eliminar"     => false,
                "perifericos.tipos.eliminar" => false,
                "historial.masivo.ver"       => true,
                _                            => true
            };
            permisos.Add(new PermisoRol { RolNombre = "TecnicoIT", AccionClave = clave, Permitido = tecnicoPermitido });

            // Consulta: solo acciones de ver/detalle/exportar
            bool consultaPermitido = clave.EndsWith(".ver") ||
                                      clave.EndsWith(".detalle") ||
                                      clave == "reportes.exportar";
            permisos.Add(new PermisoRol { RolNombre = "Consulta", AccionClave = clave, Permitido = consultaPermitido });
        }

        _db.PermisosRol.AddRange(permisos);
        await _db.SaveChangesAsync();
    }
}
