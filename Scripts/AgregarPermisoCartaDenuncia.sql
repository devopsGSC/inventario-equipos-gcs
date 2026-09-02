-- Ejecutar DESPUÉS de AgregarPermisosRoles.sql
-- Agrega el permiso para generar la carta de autorización de denuncia.
-- Idempotente: se puede correr mas de una vez sin problema.

IF NOT EXISTS (SELECT 1 FROM AccionesModulo WHERE Clave = 'empleados.cartadenuncia')
    INSERT INTO AccionesModulo (ModuloId, Clave, Nombre)
    VALUES ((SELECT Id FROM Modulos WHERE Nombre = 'Empleados'), 'empleados.cartadenuncia', 'Generar carta de autorización de denuncia');
GO

IF NOT EXISTS (SELECT 1 FROM PermisosRol WHERE AccionClave = 'empleados.cartadenuncia')
    INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido) VALUES
    ('Administrador', 'empleados.cartadenuncia', 1),
    ('TecnicoIT',      'empleados.cartadenuncia', 1),
    ('Consulta',       'empleados.cartadenuncia', 0);
GO
