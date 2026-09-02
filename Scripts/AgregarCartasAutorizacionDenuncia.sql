-- Auditoria de cartas de autorizacion para interponer aviso o denuncia
-- ante PNC / FGR por perdida, hurto o robo de equipo(s) asignado(s) a un
-- empleado. El PDF se genera al vuelo a partir de los equipos actuales
-- (EquiposIds); esta tabla solo deja constancia de quien autorizo, para
-- que empleado y cuando.
-- Idempotente: se puede correr mas de una vez sin problema.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CartasAutorizacionDenuncia')
BEGIN
    CREATE TABLE CartasAutorizacionDenuncia (
        Id                  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmpleadoId          INT NOT NULL,
        EquiposIds          NVARCHAR(200) NOT NULL,
        UsuarioAutorizaId   NVARCHAR(450) NOT NULL,
        FechaCreacion       DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        CreadoPorUsuarioId  NVARCHAR(450) NULL,
        CONSTRAINT FK_CartasAutorizacionDenuncia_Empleados_EmpleadoId
            FOREIGN KEY (EmpleadoId) REFERENCES Empleados(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_CartasAutorizacionDenuncia_AspNetUsers_UsuarioAutorizaId
            FOREIGN KEY (UsuarioAutorizaId) REFERENCES AspNetUsers(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_CartasAutorizacionDenuncia_AspNetUsers_CreadoPorUsuarioId
            FOREIGN KEY (CreadoPorUsuarioId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
    );

    CREATE INDEX IX_CartasAutorizacionDenuncia_EmpleadoId         ON CartasAutorizacionDenuncia(EmpleadoId);
    CREATE INDEX IX_CartasAutorizacionDenuncia_UsuarioAutorizaId  ON CartasAutorizacionDenuncia(UsuarioAutorizaId);
    CREATE INDEX IX_CartasAutorizacionDenuncia_CreadoPorUsuarioId ON CartasAutorizacionDenuncia(CreadoPorUsuarioId);
END
GO
