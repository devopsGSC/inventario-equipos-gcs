-- Ejecutar DESPUÉS de AgregarPermisosRoles.sql
-- Agrega el permiso para ver el historial de operaciones masivas

INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(7, 'historial.masivo.ver', 'Ver historial de operaciones masivas');

INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido) VALUES
('Administrador', 'historial.masivo.ver', 1),
('TecnicoIT',      'historial.masivo.ver', 1),
('Consulta',       'historial.masivo.ver', 0);
GO
