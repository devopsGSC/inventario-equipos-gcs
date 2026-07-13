-- =========================================================
-- AgregarMiembrosExternosYGrupos.sql
-- Permite asignar/prestar equipos y periféricos a responsables
-- que no son empleados internos: personas externas a la
-- organización (MiembrosExternos) o grupos de trabajo (Grupos,
-- ej. "Grupo Monitoreo"). Grupos es solo una referencia con
-- nombre, no gestiona membresía individual.
-- =========================================================

-- 1. Tablas nuevas
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MiembrosExternos')
BEGIN
    CREATE TABLE MiembrosExternos (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        Nombre         NVARCHAR(150) NOT NULL,
        Organizacion   NVARCHAR(150) NULL,
        Identificacion NVARCHAR(20)  NULL,
        Referencia     NVARCHAR(100) NULL,
        Activo         BIT NOT NULL DEFAULT 1
    );
    PRINT 'Tabla MiembrosExternos creada.';
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Grupos')
BEGIN
    CREATE TABLE Grupos (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        Nombre      NVARCHAR(150) NOT NULL UNIQUE,
        Descripcion NVARCHAR(150) NULL,
        Activo      BIT NOT NULL DEFAULT 1
    );
    PRINT 'Tabla Grupos creada.';
END

-- 2. Columnas + FKs en Movimientos
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Movimientos') AND name = 'MiembroExternoId'
)
BEGIN
    ALTER TABLE Movimientos ADD MiembroExternoId INT NULL;
    ALTER TABLE Movimientos ADD CONSTRAINT FK_Movimientos_MiembrosExternos
        FOREIGN KEY (MiembroExternoId) REFERENCES MiembrosExternos(Id);
    PRINT 'MiembroExternoId agregado a Movimientos.';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Movimientos') AND name = 'GrupoId'
)
BEGIN
    ALTER TABLE Movimientos ADD GrupoId INT NULL;
    ALTER TABLE Movimientos ADD CONSTRAINT FK_Movimientos_Grupos
        FOREIGN KEY (GrupoId) REFERENCES Grupos(Id);
    PRINT 'GrupoId agregado a Movimientos.';
END

-- 3. Columnas + FKs en EquiposPerifericos
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('EquiposPerifericos') AND name = 'MiembroExternoId'
)
BEGIN
    ALTER TABLE EquiposPerifericos ADD MiembroExternoId INT NULL;
    ALTER TABLE EquiposPerifericos ADD CONSTRAINT FK_EquiposPerifericos_MiembrosExternos
        FOREIGN KEY (MiembroExternoId) REFERENCES MiembrosExternos(Id);
    PRINT 'MiembroExternoId agregado a EquiposPerifericos.';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('EquiposPerifericos') AND name = 'GrupoId'
)
BEGIN
    ALTER TABLE EquiposPerifericos ADD GrupoId INT NULL;
    ALTER TABLE EquiposPerifericos ADD CONSTRAINT FK_EquiposPerifericos_Grupos
        FOREIGN KEY (GrupoId) REFERENCES Grupos(Id);
    PRINT 'GrupoId agregado a EquiposPerifericos.';
END

-- 4. Módulos nuevos
DECLARE @ModuloMiembrosExternosId INT;
DECLARE @ModuloGruposId INT;

IF NOT EXISTS (SELECT 1 FROM Modulos WHERE Nombre = 'Miembros Externos')
BEGIN
    INSERT INTO Modulos (Nombre, Icono) VALUES ('Miembros Externos', 'bi-person-badge');
    SET @ModuloMiembrosExternosId = SCOPE_IDENTITY();
END
ELSE
    SELECT @ModuloMiembrosExternosId = Id FROM Modulos WHERE Nombre = 'Miembros Externos';

IF NOT EXISTS (SELECT 1 FROM Modulos WHERE Nombre = 'Grupos')
BEGIN
    INSERT INTO Modulos (Nombre, Icono) VALUES ('Grupos', 'bi-diagram-3');
    SET @ModuloGruposId = SCOPE_IDENTITY();
END
ELSE
    SELECT @ModuloGruposId = Id FROM Modulos WHERE Nombre = 'Grupos';

-- 5. Acciones (permisos) nuevas
IF NOT EXISTS (SELECT 1 FROM AccionesModulo WHERE Clave = 'miembrosexternos.ver')
BEGIN
    INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
    (@ModuloMiembrosExternosId, 'miembrosexternos.ver',     'Ver listado de miembros externos'),
    (@ModuloMiembrosExternosId, 'miembrosexternos.detalle', 'Ver detalle de miembro externo'),
    (@ModuloMiembrosExternosId, 'miembrosexternos.crear',   'Registrar miembro externo'),
    (@ModuloMiembrosExternosId, 'miembrosexternos.editar',  'Editar miembro externo');
    PRINT 'Acciones de Miembros Externos agregadas.';
END

IF NOT EXISTS (SELECT 1 FROM AccionesModulo WHERE Clave = 'grupos.ver')
BEGIN
    INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
    (@ModuloGruposId, 'grupos.ver',     'Ver listado de grupos'),
    (@ModuloGruposId, 'grupos.detalle', 'Ver detalle de grupo'),
    (@ModuloGruposId, 'grupos.crear',   'Registrar grupo'),
    (@ModuloGruposId, 'grupos.editar',  'Editar grupo');
    PRINT 'Acciones de Grupos agregadas.';
END

-- 6. Permisos por rol
IF NOT EXISTS (SELECT 1 FROM PermisosRol WHERE AccionClave = 'miembrosexternos.ver')
BEGIN
    INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido) VALUES
    ('Administrador', 'miembrosexternos.ver',     1),
    ('Administrador', 'miembrosexternos.detalle', 1),
    ('Administrador', 'miembrosexternos.crear',   1),
    ('Administrador', 'miembrosexternos.editar',  1),
    ('TecnicoIT',     'miembrosexternos.ver',     1),
    ('TecnicoIT',     'miembrosexternos.detalle', 1),
    ('TecnicoIT',     'miembrosexternos.crear',   1),
    ('TecnicoIT',     'miembrosexternos.editar',  1),
    ('Consulta',      'miembrosexternos.ver',     1),
    ('Consulta',      'miembrosexternos.detalle', 1),
    ('Consulta',      'miembrosexternos.crear',   0),
    ('Consulta',      'miembrosexternos.editar',  0);
    PRINT 'Permisos de Miembros Externos agregados.';
END

IF NOT EXISTS (SELECT 1 FROM PermisosRol WHERE AccionClave = 'grupos.ver')
BEGIN
    INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido) VALUES
    ('Administrador', 'grupos.ver',     1),
    ('Administrador', 'grupos.detalle', 1),
    ('Administrador', 'grupos.crear',   1),
    ('Administrador', 'grupos.editar',  1),
    ('TecnicoIT',     'grupos.ver',     1),
    ('TecnicoIT',     'grupos.detalle', 1),
    ('TecnicoIT',     'grupos.crear',   1),
    ('TecnicoIT',     'grupos.editar',  1),
    ('Consulta',      'grupos.ver',     1),
    ('Consulta',      'grupos.detalle', 1),
    ('Consulta',      'grupos.crear',   0),
    ('Consulta',      'grupos.editar',  0);
    PRINT 'Permisos de Grupos agregados.';
END
GO
