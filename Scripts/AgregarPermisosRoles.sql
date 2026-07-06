-- Tabla de módulos del sistema
CREATE TABLE Modulos (
    Id     INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL,  -- "Equipos", "Perifericos", "Empleados", etc.
    Icono  NVARCHAR(50)  NOT NULL DEFAULT 'bi-grid'
);

-- Tabla de acciones posibles por módulo
CREATE TABLE AccionesModulo (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    ModuloId INT NOT NULL FOREIGN KEY REFERENCES Modulos(Id),
    Clave    NVARCHAR(100) NOT NULL,  -- "equipos.ver", "equipos.crear", "equipos.editar"
    Nombre   NVARCHAR(100) NOT NULL,  -- "Ver equipos", "Crear equipo", "Editar equipo"
    Descripcion NVARCHAR(255) NULL
);

-- Tabla de permisos por rol (qué acciones tiene cada rol)
CREATE TABLE PermisosRol (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    RolNombre   NVARCHAR(256) NOT NULL,  -- "Administrador", "TecnicoIT", "Consulta"
    AccionClave NVARCHAR(100) NOT NULL,  -- "equipos.crear"
    Permitido   BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_PermisoRol UNIQUE (RolNombre, AccionClave)
);

-- Seed: módulos del sistema
INSERT INTO Modulos (Nombre, Icono) VALUES
('Equipos',       'bi-laptop'),
('Periféricos',   'bi-plug'),
('Empleados',     'bi-people'),
('Movimientos',   'bi-arrow-left-right'),
('Reportes',      'bi-bar-chart'),
('Carga Masiva',  'bi-file-earmark-arrow-up'),
('Usuarios',      'bi-person-gear');

-- Seed: acciones por módulo
-- Equipos
INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(1, 'equipos.ver',        'Ver listado de equipos'),
(1, 'equipos.detalle',    'Ver detalle de equipo'),
(1, 'equipos.crear',      'Registrar nuevo equipo'),
(1, 'equipos.editar',     'Editar equipo'),
(1, 'equipos.baja',       'Dar de baja / reactivar'),
(1, 'equipos.cargamasiva','Carga masiva de equipos');
-- Periféricos
INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(2, 'perifericos.ver',    'Ver listado de periféricos'),
(2, 'perifericos.detalle','Ver detalle de periférico'),
(2, 'perifericos.crear',  'Registrar periférico'),
(2, 'perifericos.editar', 'Editar periférico'),
(2, 'perifericos.baja',   'Dar de baja / reactivar'),
(2, 'perifericos.asignar','Asignar periférico a empleado');
-- Empleados
INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(3, 'empleados.ver',      'Ver listado de empleados'),
(3, 'empleados.detalle',  'Ver detalle de empleado'),
(3, 'empleados.crear',    'Registrar empleado'),
(3, 'empleados.editar',   'Editar empleado');
-- Movimientos
INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(4, 'movimientos.ver',    'Ver historial de movimientos'),
(4, 'movimientos.asignar','Registrar asignación'),
(4, 'movimientos.prestamo','Registrar préstamo'),
(4, 'movimientos.devolucion','Registrar devolución'),
(4, 'movimientos.garantia','Registrar entrada/salida de garantía'),
(4, 'movimientos.carta',  'Descargar cartas PDF'),
(4, 'movimientos.finiquito','Generar finiquito');
-- Reportes
INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(5, 'reportes.ver',       'Ver reportes'),
(5, 'reportes.exportar',  'Exportar PDF / Excel / CSV');
-- Carga masiva
INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(6, 'cargamasiva.usar',   'Usar carga masiva');
-- Usuarios (solo admin)
INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(7, 'usuarios.ver',       'Ver usuarios'),
(7, 'usuarios.crear',     'Crear usuario'),
(7, 'usuarios.editar',    'Editar usuario'),
(7, 'usuarios.permisos',  'Gestionar permisos de roles');

-- Seed: permisos por defecto para cada rol
-- Administrador: TODO permitido
INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido)
SELECT 'Administrador', Clave, 1 FROM AccionesModulo;

-- TecnicoIT: todo excepto gestión de usuarios y permisos
INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido)
SELECT 'TecnicoIT', Clave,
    CASE WHEN Clave IN ('usuarios.ver','usuarios.crear','usuarios.editar','usuarios.permisos')
         THEN 0 ELSE 1 END
FROM AccionesModulo;

-- Consulta: solo ver y exportar, sin crear ni modificar nada
INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido)
SELECT 'Consulta', Clave,
    CASE WHEN Clave LIKE '%.ver' OR Clave LIKE '%.detalle' OR Clave = 'reportes.exportar'
         THEN 1 ELSE 0 END
FROM AccionesModulo;
GO
