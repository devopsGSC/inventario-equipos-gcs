-- =========================================================
-- AgregarAuditoriaActividad.sql
-- Agrega "quién hizo la acción" (CreadoPorUsuarioId -> AspNetUsers)
-- a las tablas que participan del feed de actividad reciente del
-- panel general, y agrega FechaRegistro a Empleados/MiembrosExternos/
-- Grupos, que hasta ahora no tenían ninguna fecha de creación.
--
-- La atribución de usuario solo aplica a partir de que se corra este
-- script: los registros existentes quedan con CreadoPorUsuarioId NULL
-- (no se puede reconstruir quién hizo algo en el pasado).
--
-- ON DELETE SET NULL: si se borra la cuenta de un usuario de IT, su
-- actividad histórica se conserva, solo se pierde la atribución.
--
-- Idempotente: se puede correr mas de una vez sin problema.
-- =========================================================

-- ===== FechaRegistro (Empleados, MiembrosExternos, Grupos) =====

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Empleados') AND name = 'FechaRegistro'
)
    ALTER TABLE Empleados ADD FechaRegistro DATETIME2 NOT NULL DEFAULT SYSDATETIME();
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('MiembrosExternos') AND name = 'FechaRegistro'
)
    ALTER TABLE MiembrosExternos ADD FechaRegistro DATETIME2 NOT NULL DEFAULT SYSDATETIME();
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Grupos') AND name = 'FechaRegistro'
)
    ALTER TABLE Grupos ADD FechaRegistro DATETIME2 NOT NULL DEFAULT SYSDATETIME();
GO

-- ===== CreadoPorUsuarioId (las 8 tablas) =====

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Equipos') AND name = 'CreadoPorUsuarioId')
    ALTER TABLE Equipos ADD CreadoPorUsuarioId NVARCHAR(450) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Equipos_AspNetUsers_CreadoPorUsuarioId')
    ALTER TABLE Equipos ADD CONSTRAINT FK_Equipos_AspNetUsers_CreadoPorUsuarioId
        FOREIGN KEY (CreadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Perifericos') AND name = 'CreadoPorUsuarioId')
    ALTER TABLE Perifericos ADD CreadoPorUsuarioId NVARCHAR(450) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Perifericos_AspNetUsers_CreadoPorUsuarioId')
    ALTER TABLE Perifericos ADD CONSTRAINT FK_Perifericos_AspNetUsers_CreadoPorUsuarioId
        FOREIGN KEY (CreadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Movimientos') AND name = 'CreadoPorUsuarioId')
    ALTER TABLE Movimientos ADD CreadoPorUsuarioId NVARCHAR(450) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Movimientos_AspNetUsers_CreadoPorUsuarioId')
    ALTER TABLE Movimientos ADD CONSTRAINT FK_Movimientos_AspNetUsers_CreadoPorUsuarioId
        FOREIGN KEY (CreadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EquiposPerifericos') AND name = 'CreadoPorUsuarioId')
    ALTER TABLE EquiposPerifericos ADD CreadoPorUsuarioId NVARCHAR(450) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EquiposPerifericos_AspNetUsers_CreadoPorUsuarioId')
    ALTER TABLE EquiposPerifericos ADD CONSTRAINT FK_EquiposPerifericos_AspNetUsers_CreadoPorUsuarioId
        FOREIGN KEY (CreadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LicenciasAsignaciones') AND name = 'CreadoPorUsuarioId')
    ALTER TABLE LicenciasAsignaciones ADD CreadoPorUsuarioId NVARCHAR(450) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_LicenciasAsignaciones_AspNetUsers_CreadoPorUsuarioId')
    ALTER TABLE LicenciasAsignaciones ADD CONSTRAINT FK_LicenciasAsignaciones_AspNetUsers_CreadoPorUsuarioId
        FOREIGN KEY (CreadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Empleados') AND name = 'CreadoPorUsuarioId')
    ALTER TABLE Empleados ADD CreadoPorUsuarioId NVARCHAR(450) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Empleados_AspNetUsers_CreadoPorUsuarioId')
    ALTER TABLE Empleados ADD CONSTRAINT FK_Empleados_AspNetUsers_CreadoPorUsuarioId
        FOREIGN KEY (CreadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('MiembrosExternos') AND name = 'CreadoPorUsuarioId')
    ALTER TABLE MiembrosExternos ADD CreadoPorUsuarioId NVARCHAR(450) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MiembrosExternos_AspNetUsers_CreadoPorUsuarioId')
    ALTER TABLE MiembrosExternos ADD CONSTRAINT FK_MiembrosExternos_AspNetUsers_CreadoPorUsuarioId
        FOREIGN KEY (CreadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Grupos') AND name = 'CreadoPorUsuarioId')
    ALTER TABLE Grupos ADD CreadoPorUsuarioId NVARCHAR(450) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Grupos_AspNetUsers_CreadoPorUsuarioId')
    ALTER TABLE Grupos ADD CONSTRAINT FK_Grupos_AspNetUsers_CreadoPorUsuarioId
        FOREIGN KEY (CreadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
GO
