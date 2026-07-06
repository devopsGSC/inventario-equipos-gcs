-- =========================================================
-- AgregarEspecificacionesEquipo.sql
-- Especificaciones técnicas generales (RAM, Procesador,
-- Almacenamiento) para todos los tipos de equipo, y Plan de
-- Datos (catálogo editable) específico para teléfonos.
-- =========================================================

-- Especificaciones técnicas generales
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Equipos') AND name = 'RAM')
    ALTER TABLE Equipos ADD RAM NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Equipos') AND name = 'Procesador')
    ALTER TABLE Equipos ADD Procesador NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Equipos') AND name = 'Almacenamiento')
    ALTER TABLE Equipos ADD Almacenamiento NVARCHAR(50) NULL;

-- Tabla de planes de datos (para teléfonos)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PlanesData')
BEGIN
    CREATE TABLE PlanesData (
        Id     INT IDENTITY(1,1) PRIMARY KEY,
        Nombre NVARCHAR(100) NOT NULL UNIQUE
    );
    -- Seed con los planes iniciales
    INSERT INTO PlanesData (Nombre) VALUES
    ('ESP 48'),
    ('ESP 23'),
    ('ESP 15');
    PRINT 'Tabla PlanesData creada con seed inicial.';
END

-- FK de Plan de Datos en Equipos (solo aplica para teléfonos)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Equipos') AND name = 'PlanDataId')
BEGIN
    ALTER TABLE Equipos ADD PlanDataId INT NULL;
    ALTER TABLE Equipos ADD CONSTRAINT FK_Equipos_PlanesData
        FOREIGN KEY (PlanDataId) REFERENCES PlanesData(Id) ON DELETE SET NULL;
    PRINT 'PlanDataId agregado a Equipos.';
END

-- Permisos para gestión de planes de datos (módulo Equipos = 1)
INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(1, 'equipos.planes.crear',   'Crear plan de datos personalizado'),
(1, 'equipos.planes.eliminar','Eliminar plan de datos personalizado');

INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido) VALUES
('Administrador', 'equipos.planes.crear',    1),
('Administrador', 'equipos.planes.eliminar', 1),
('TecnicoIT',     'equipos.planes.crear',    1),
('TecnicoIT',     'equipos.planes.eliminar', 0),
('Consulta',      'equipos.planes.crear',    0),
('Consulta',      'equipos.planes.eliminar', 0);
GO
