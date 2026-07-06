-- =========================================================
-- AgregarSitioActivo.sql
-- Los sitios se desactivan (borrado lógico) en vez de
-- eliminarse: preserva la integridad de movimientos y
-- asignaciones que ya los referencian.
-- =========================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Sitios') AND name = 'Activo'
)
BEGIN
    ALTER TABLE Sitios ADD Activo BIT NOT NULL DEFAULT 1;
    PRINT 'Activo agregado a Sitios.';
END
GO
