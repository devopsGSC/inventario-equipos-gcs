-- =========================================================
-- AgregarLicencias.sql
-- Modulo de licencias de software: catalogo de tipos de
-- licencia (sin identificador unico por unidad, se maneja
-- como pool/contador por tipo) y sus asignaciones a
-- Empleado / MiembroExterno / Grupo, directas o adjuntas a
-- la asignacion/prestamo de un Equipo.
-- =========================================================

-- 1. Tablas nuevas
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TiposLicencia')
BEGIN
    CREATE TABLE TiposLicencia (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        Nombre        NVARCHAR(150) NOT NULL UNIQUE,
        CantidadTotal INT NULL,
        Activo        BIT NOT NULL DEFAULT 1
    );
    PRINT 'Tabla TiposLicencia creada.';
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LicenciasAsignaciones')
BEGIN
    CREATE TABLE LicenciasAsignaciones (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        TipoLicenciaId      INT NOT NULL,
        EquipoId            INT NULL,
        EmpleadoId          INT NULL,
        MiembroExternoId    INT NULL,
        GrupoId             INT NULL,
        TipoAsignacion      NVARCHAR(20) NOT NULL DEFAULT 'Directo',
        TipoMovimiento      NVARCHAR(20) NOT NULL DEFAULT 'Asignacion',
        FechaAsignacion     DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        FechaDesvinculacion DATETIME2 NULL,
        Observaciones       NVARCHAR(500) NULL,
        CONSTRAINT FK_LicenciasAsignaciones_TiposLicencia
            FOREIGN KEY (TipoLicenciaId) REFERENCES TiposLicencia(Id),
        CONSTRAINT FK_LicenciasAsignaciones_Equipos
            FOREIGN KEY (EquipoId) REFERENCES Equipos(Id),
        CONSTRAINT FK_LicenciasAsignaciones_Empleados
            FOREIGN KEY (EmpleadoId) REFERENCES Empleados(Id),
        CONSTRAINT FK_LicenciasAsignaciones_MiembrosExternos
            FOREIGN KEY (MiembroExternoId) REFERENCES MiembrosExternos(Id),
        CONSTRAINT FK_LicenciasAsignaciones_Grupos
            FOREIGN KEY (GrupoId) REFERENCES Grupos(Id)
    );
    PRINT 'Tabla LicenciasAsignaciones creada.';
END

-- 2. Tipos de licencia predeterminados
IF NOT EXISTS (SELECT 1 FROM TiposLicencia WHERE Nombre = 'Microsoft 365 Empresa Básico')
BEGIN
    INSERT INTO TiposLicencia (Nombre) VALUES
    ('Microsoft 365 Empresa Básico'),
    ('Microsoft 365 Empresa Premium'),
    ('Planner y Project Plan 3'),
    ('Power BI Premium por usuario'),
    ('Visio Plan 2');
    PRINT 'Tipos de licencia predeterminados agregados.';
END

-- 3. Modulo nuevo
DECLARE @ModuloLicenciasId INT;

IF NOT EXISTS (SELECT 1 FROM Modulos WHERE Nombre = 'Licencias')
BEGIN
    INSERT INTO Modulos (Nombre, Icono) VALUES ('Licencias', 'bi-card-checklist');
    SET @ModuloLicenciasId = SCOPE_IDENTITY();
END
ELSE
    SELECT @ModuloLicenciasId = Id FROM Modulos WHERE Nombre = 'Licencias';

-- 4. Acciones (permisos) nuevas
IF NOT EXISTS (SELECT 1 FROM AccionesModulo WHERE Clave = 'licencias.ver')
BEGIN
    INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
    (@ModuloLicenciasId, 'licencias.ver',     'Ver listado de licencias'),
    (@ModuloLicenciasId, 'licencias.detalle', 'Ver detalle de licencia'),
    (@ModuloLicenciasId, 'licencias.crear',   'Registrar tipo de licencia'),
    (@ModuloLicenciasId, 'licencias.editar',  'Editar tipo de licencia'),
    (@ModuloLicenciasId, 'licencias.asignar', 'Asignar/revocar licencia');
    PRINT 'Acciones de Licencias agregadas.';
END

-- 5. Permisos por rol
IF NOT EXISTS (SELECT 1 FROM PermisosRol WHERE AccionClave = 'licencias.ver')
BEGIN
    INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido) VALUES
    ('Administrador', 'licencias.ver',     1),
    ('Administrador', 'licencias.detalle', 1),
    ('Administrador', 'licencias.crear',   1),
    ('Administrador', 'licencias.editar',  1),
    ('Administrador', 'licencias.asignar', 1),
    ('TecnicoIT',     'licencias.ver',     1),
    ('TecnicoIT',     'licencias.detalle', 1),
    ('TecnicoIT',     'licencias.crear',   1),
    ('TecnicoIT',     'licencias.editar',  1),
    ('TecnicoIT',     'licencias.asignar', 1),
    ('Consulta',      'licencias.ver',     1),
    ('Consulta',      'licencias.detalle', 1),
    ('Consulta',      'licencias.crear',   0),
    ('Consulta',      'licencias.editar',  0),
    ('Consulta',      'licencias.asignar', 0);
    PRINT 'Permisos de Licencias agregados.';
END
GO
