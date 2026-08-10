-- =========================================================
-- AgregarEntregaPorUsuario.sql
-- Distingue entre quién REGISTRÓ una asignación en el sistema
-- (CreadoPorUsuarioId, ya existente) y quién realmente hizo la
-- ENTREGA física del equipo/periférico y firmó la carta por parte
-- de TI (puede ser una persona distinta: alguien asigna, otra
-- persona entrega y hace firmar al colaborador).
--
-- EntregaCompletada queda en 0 al crear el movimiento/asignación.
-- La PRIMERA vez que alguien descarga la carta para la entrega
-- física, el sistema fija EntregadoPorUsuarioId/FechaEntrega con
-- quien la está descargando en ese momento y pone
-- EntregaCompletada = 1. Descargas posteriores (reimpresiones,
-- otra persona revisando) ya no cambian esa atribución.
--
-- También se amplía Observaciones en Movimientos de 500 a 2000
-- caracteres, porque ahora se le van agregando notas con fecha y
-- usuario cada vez que alguien interviene en el movimiento (p.ej.
-- "listo para que Fulano haga la recepción", "equipo entregado").
--
-- Movimientos y EquiposPerifericos ya tienen una FK hacia AspNetUsers
-- con ON DELETE SET NULL (CreadoPorUsuarioId). SQL Server no permite una
-- segunda FK con acción de cascada hacia la misma tabla padre desde la
-- misma tabla hija ("multiple cascade paths"), así que esta FK nueva usa
-- ON DELETE NO ACTION: si se intenta borrar la cuenta de un usuario de IT
-- que quedó registrado como "entregó" algo, hay que reasignar/limpiar esa
-- referencia primero (la atribución en CreadoPorUsuarioId sigue
-- limpiándose sola como siempre).
--
-- Idempotente: se puede correr mas de una vez sin problema.
-- =========================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Movimientos') AND name = 'EntregadoPorUsuarioId')
    ALTER TABLE Movimientos ADD EntregadoPorUsuarioId NVARCHAR(450) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Movimientos_AspNetUsers_EntregadoPorUsuarioId')
    ALTER TABLE Movimientos ADD CONSTRAINT FK_Movimientos_AspNetUsers_EntregadoPorUsuarioId
        FOREIGN KEY (EntregadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Movimientos') AND name = 'EntregaCompletada')
    ALTER TABLE Movimientos ADD EntregaCompletada BIT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Movimientos') AND name = 'FechaEntrega')
    ALTER TABLE Movimientos ADD FechaEntrega DATETIME2 NULL;
GO
ALTER TABLE Movimientos ALTER COLUMN Observaciones NVARCHAR(2000) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EquiposPerifericos') AND name = 'EntregadoPorUsuarioId')
    ALTER TABLE EquiposPerifericos ADD EntregadoPorUsuarioId NVARCHAR(450) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EquiposPerifericos_AspNetUsers_EntregadoPorUsuarioId')
    ALTER TABLE EquiposPerifericos ADD CONSTRAINT FK_EquiposPerifericos_AspNetUsers_EntregadoPorUsuarioId
        FOREIGN KEY (EntregadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EquiposPerifericos') AND name = 'EntregaCompletada')
    ALTER TABLE EquiposPerifericos ADD EntregaCompletada BIT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EquiposPerifericos') AND name = 'FechaEntrega')
    ALTER TABLE EquiposPerifericos ADD FechaEntrega DATETIME2 NULL;
GO
