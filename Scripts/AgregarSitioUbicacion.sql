-- =========================================================
-- AgregarSitioUbicacion.sql
-- Sitio/Ubicación se registra por movimiento (asignación,
-- préstamo, etc.), no como atributo fijo del equipo/periférico.
-- Referencia interna del sistema: nunca aparece en cartas PDF.
-- =========================================================

-- Tabla de sitios compartida
CREATE TABLE Sitios (
    Id     INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(150) NOT NULL UNIQUE
);

INSERT INTO Sitios (Nombre) VALUES
('Oficina Principal - Santa Elena'),
('Bodega Central'),
('Piso 1'),
('Piso 2'),
('Sala de Servidores');

-- Sitio en movimientos de equipos
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Movimientos') AND name = 'SitioId'
)
BEGIN
    ALTER TABLE Movimientos ADD SitioId INT NULL;
    ALTER TABLE Movimientos ADD CONSTRAINT FK_Movimientos_Sitios
        FOREIGN KEY (SitioId) REFERENCES Sitios(Id);
    PRINT 'SitioId agregado a Movimientos.';
END

-- Sitio en movimientos de periféricos directos
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('EquiposPerifericos') AND name = 'SitioId'
)
BEGIN
    ALTER TABLE EquiposPerifericos ADD SitioId INT NULL;
    ALTER TABLE EquiposPerifericos ADD CONSTRAINT FK_EquiposPerifericos_Sitios
        FOREIGN KEY (SitioId) REFERENCES Sitios(Id);
    PRINT 'SitioId agregado a EquiposPerifericos.';
END

-- Permisos
INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(4, 'movimientos.sitio',           'Seleccionar sitio al registrar movimiento'),
(4, 'sitios.crear',                'Crear sitio personalizado'),
(4, 'sitios.eliminar',             'Eliminar sitio personalizado');

INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido) VALUES
('Administrador', 'movimientos.sitio',  1),
('Administrador', 'sitios.crear',       1),
('Administrador', 'sitios.eliminar',    1),
('TecnicoIT',     'movimientos.sitio',  1),
('TecnicoIT',     'sitios.crear',       1),
('TecnicoIT',     'sitios.eliminar',    0),
('Consulta',      'movimientos.sitio',  0),
('Consulta',      'sitios.crear',       0),
('Consulta',      'sitios.eliminar',    0);
GO
