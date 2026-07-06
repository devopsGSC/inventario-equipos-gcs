-- =========================================================
-- AgregarMovimientosPeriferico.sql
-- Agrega TipoMovimiento (Asignacion/Prestamo/Devolucion) a
-- EquiposPerifericos para que los periféricos con asignación
-- directa tengan el mismo flujo de movimientos que los equipos.
-- =========================================================


-- Agregar TipoMovimiento a EquiposPerifericos para diferenciar Asignacion/Prestamo/Devolucion
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('EquiposPerifericos') AND name = 'TipoMovimiento'
)
BEGIN
    ALTER TABLE EquiposPerifericos
    ADD TipoMovimiento NVARCHAR(20) NOT NULL DEFAULT 'Asignacion';
    PRINT 'Columna TipoMovimiento agregada.';
END

-- Agregar FechaDevolucionEstimada para préstamos
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('EquiposPerifericos') AND name = 'FechaDevolucionEstimada'
)
BEGIN
    ALTER TABLE EquiposPerifericos
    ADD FechaDevolucionEstimada DATE NULL;
    PRINT 'Columna FechaDevolucionEstimada agregada.';
END

-- Agregar Observaciones
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('EquiposPerifericos') AND name = 'Observaciones'
)
BEGIN
    ALTER TABLE EquiposPerifericos
    ADD Observaciones NVARCHAR(500) NULL;
    PRINT 'Columna Observaciones agregada.';
END
GO
