-- =========================================================
-- AgregarSolicitudesFirma.sql
-- Links de un solo uso para que un colaborador firme un
-- movimiento a distancia cuando no está físicamente presente.
-- =========================================================

CREATE TABLE SolicitudesFirma (
    Id              INT           IDENTITY PRIMARY KEY,
    MovimientoId    INT           NOT NULL,
    Token           NVARCHAR(64)  NOT NULL,
    FechaCreacion   DATETIME2     NOT NULL,
    FechaExpiracion DATETIME2     NOT NULL,
    FechaFirmado    DATETIME2     NULL,
    CONSTRAINT FK_SolicitudesFirma_Movimientos_MovimientoId
        FOREIGN KEY (MovimientoId) REFERENCES Movimientos(Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IX_SolicitudesFirma_Token ON SolicitudesFirma(Token);
