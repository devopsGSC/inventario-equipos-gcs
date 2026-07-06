-- Tabla principal: cada operación masiva (una por archivo subido)
CREATE TABLE OperacionesMasivas (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TipoOperacion   NVARCHAR(20)  NOT NULL, -- 'CargaMasiva' | 'ActualizacionMasiva'
    FechaOperacion  DATETIME2     NOT NULL DEFAULT GETDATE(),
    UsuarioNombre   NVARCHAR(256) NOT NULL, -- email o nombre del usuario logueado
    NombreArchivo   NVARCHAR(255) NOT NULL,
    TotalProcesados INT           NOT NULL DEFAULT 0,
    TotalExitosos   INT           NOT NULL DEFAULT 0,
    TotalOmitidos   INT           NOT NULL DEFAULT 0,
    TotalErrores    INT           NOT NULL DEFAULT 0,
    Observaciones   NVARCHAR(500) NULL      -- notas adicionales si aplica
);

-- Tabla de detalle: una fila por equipo procesado en cada operación
CREATE TABLE DetalleOperacionMasiva (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    OperacionId      INT           NOT NULL FOREIGN KEY REFERENCES OperacionesMasivas(Id) ON DELETE CASCADE,
    FilaExcel        INT           NOT NULL,
    NumeroSerie      NVARCHAR(100) NOT NULL,
    NombreEquipo     NVARCHAR(200) NULL,
    Estado           NVARCHAR(20)  NOT NULL, -- 'OK' | 'Omitido' | 'Error' | 'Advertencia' | 'SinCambios'
    Mensaje          NVARCHAR(500) NULL,
    CamposModificados NVARCHAR(500) NULL     -- solo para ActualizacionMasiva: "Nombre, Modelo, Garantía"
);

CREATE INDEX IX_OperacionesMasivas_Fecha    ON OperacionesMasivas(FechaOperacion DESC);
CREATE INDEX IX_OperacionesMasivas_Usuario  ON OperacionesMasivas(UsuarioNombre);
CREATE INDEX IX_DetalleOperacion_OperacId   ON DetalleOperacionMasiva(OperacionId);
GO
