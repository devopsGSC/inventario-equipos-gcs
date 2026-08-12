-- =========================================================
-- AgregarCartaGeneral.sql
-- Crea la tabla CartasGenerales: un documento firmable que resume
-- TODOS los equipos y perifericos actuales de un empleado, miembro
-- externo o grupo (a diferencia de la carta de un solo movimiento).
-- Tiene el mismo ciclo de vida que un movimiento: se prepara,
-- alguien la entrega y firma (fisico o por link remoto), y queda
-- en el historial con su propia atribucion.
--
-- Tambien agrega CartaGeneralId a SolicitudesFirma para poder
-- generar links de firma remota de esta carta, igual que ya se
-- hace con movimientos y asignaciones directas de periferico.
--
-- NOTA: si ya corriste una version anterior de este script, la tabla
-- pudo haber quedado creada como "CartaGenerales" (singular) por un
-- error de nombre; este script la renombra sola a "CartasGenerales"
-- (que es como espera encontrarla el DbSet de EF Core) antes de
-- seguir, sin perder los datos que ya tuviera.
--
-- Idempotente: se puede correr mas de una vez sin problema.
-- =========================================================

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CartaGenerales')
   AND NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CartasGenerales')
    EXEC sp_rename 'CartaGenerales', 'CartasGenerales';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CartasGenerales')
BEGIN
    CREATE TABLE CartasGenerales (
        Id                    INT IDENTITY PRIMARY KEY,
        EmpleadoId            INT NULL,
        MiembroExternoId      INT NULL,
        GrupoId               INT NULL,
        FechaCreacion         DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        Observaciones         NVARCHAR(2000) NULL,
        FirmaEmpleado         NVARCHAR(MAX) NULL,
        CartaGenerada         BIT NOT NULL DEFAULT 0,
        CreadoPorUsuarioId    NVARCHAR(450) NULL,
        EntregadoPorUsuarioId NVARCHAR(450) NULL,
        EntregaCompletada     BIT NOT NULL DEFAULT 0,
        FechaEntrega          DATETIME2 NULL,
        CONSTRAINT FK_CartasGenerales_Empleados_EmpleadoId
            FOREIGN KEY (EmpleadoId) REFERENCES Empleados(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_CartasGenerales_MiembrosExternos_MiembroExternoId
            FOREIGN KEY (MiembroExternoId) REFERENCES MiembrosExternos(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_CartasGenerales_Grupos_GrupoId
            FOREIGN KEY (GrupoId) REFERENCES Grupos(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_CartasGenerales_AspNetUsers_CreadoPorUsuarioId
            FOREIGN KEY (CreadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
        CONSTRAINT FK_CartasGenerales_AspNetUsers_EntregadoPorUsuarioId
            FOREIGN KEY (EntregadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SolicitudesFirma') AND name = 'CartaGeneralId')
    ALTER TABLE SolicitudesFirma ADD CartaGeneralId INT NULL;
GO
-- NO ACTION (no CASCADE): CartasGenerales ya tiene su propia FK con
-- SET NULL hacia AspNetUsers, y sumar aqui una ruta en cascada crea
-- "multiple cascade paths" desde AspNetUsers hasta SolicitudesFirma
-- (el mismo problema que ya resolvimos para EntregadoPorUsuarioId).
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SolicitudesFirma_CartasGenerales_CartaGeneralId')
    ALTER TABLE SolicitudesFirma ADD CONSTRAINT FK_SolicitudesFirma_CartasGenerales_CartaGeneralId
        FOREIGN KEY (CartaGeneralId) REFERENCES CartasGenerales(Id) ON DELETE NO ACTION;
GO

-- CK_SolicitudesFirma_UnaReferencia exigia exactamente una de dos
-- referencias (Movimiento o EquipoPeriferico). Ahora que hay una
-- tercera (CartaGeneral), hay que rehacerla para que siga exigiendo
-- "exactamente una de las tres", si no cualquier solicitud nueva de
-- carta general viola el check viejo.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_SolicitudesFirma_UnaReferencia')
    ALTER TABLE SolicitudesFirma DROP CONSTRAINT CK_SolicitudesFirma_UnaReferencia;
GO
ALTER TABLE SolicitudesFirma
    ADD CONSTRAINT CK_SolicitudesFirma_UnaReferencia
    CHECK (
        (CASE WHEN MovimientoId IS NOT NULL THEN 1 ELSE 0 END) +
        (CASE WHEN EquipoPerifericoId IS NOT NULL THEN 1 ELSE 0 END) +
        (CASE WHEN CartaGeneralId IS NOT NULL THEN 1 ELSE 0 END) = 1
    );
GO
