IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('EquiposPerifericos') AND name = 'FotoPeriferico'
)
BEGIN
    ALTER TABLE EquiposPerifericos ADD FotoPeriferico NVARCHAR(MAX) NULL;
    PRINT 'Columna FotoPeriferico agregada a EquiposPerifericos.';
END
GO
