-- =========================================================
-- AgregarSolicitudFirmaPeriferico.sql
-- Permite que SolicitudesFirma también apunte a una asignación
-- directa de periférico (EquiposPerifericos), no solo a un
-- movimiento de equipo. Exactamente una de las dos FK va llena.
--
-- Idempotente: se puede volver a correr sin problema aunque un
-- intento anterior haya quedado a medias.
-- =========================================================

-- 1. MovimientoId pasa a nullable (drop FK, alter, re-add FK)
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_SolicitudesFirma_Movimientos_MovimientoId'
)
    ALTER TABLE SolicitudesFirma
        DROP CONSTRAINT FK_SolicitudesFirma_Movimientos_MovimientoId;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('SolicitudesFirma') AND name = 'MovimientoId' AND is_nullable = 0
)
    ALTER TABLE SolicitudesFirma ALTER COLUMN MovimientoId INT NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_SolicitudesFirma_Movimientos_MovimientoId'
)
    ALTER TABLE SolicitudesFirma
        ADD CONSTRAINT FK_SolicitudesFirma_Movimientos_MovimientoId
        FOREIGN KEY (MovimientoId) REFERENCES Movimientos(Id) ON DELETE CASCADE;
GO

-- 2. Nueva columna + FK a EquiposPerifericos
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('SolicitudesFirma') AND name = 'EquipoPerifericoId'
)
    ALTER TABLE SolicitudesFirma ADD EquipoPerifericoId INT NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_SolicitudesFirma_EquiposPerifericos_EquipoPerifericoId'
)
    ALTER TABLE SolicitudesFirma
        ADD CONSTRAINT FK_SolicitudesFirma_EquiposPerifericos_EquipoPerifericoId
        FOREIGN KEY (EquipoPerifericoId) REFERENCES EquiposPerifericos(Id) ON DELETE CASCADE;
GO

-- 3. Exactamente una de las dos referencias debe estar llena
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_SolicitudesFirma_UnaReferencia'
)
    ALTER TABLE SolicitudesFirma
        ADD CONSTRAINT CK_SolicitudesFirma_UnaReferencia
        CHECK (
            (MovimientoId IS NOT NULL AND EquipoPerifericoId IS NULL) OR
            (MovimientoId IS NULL AND EquipoPerifericoId IS NOT NULL)
        );
GO
