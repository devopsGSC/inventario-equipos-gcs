-- Agrega el DUI del usuario del sistema, necesario para que pueda
-- figurar como "quien autoriza" en la carta de autorizacion de denuncia.
-- Idempotente: se puede correr mas de una vez sin problema.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'DUI')
    ALTER TABLE AspNetUsers ADD DUI NVARCHAR(20) NULL;
GO
