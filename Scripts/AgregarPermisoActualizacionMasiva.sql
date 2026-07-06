-- Ejecutar DESPUÉS de AgregarPermisosRoles.sql
-- Agrega el permiso del nuevo módulo de Actualización Masiva (solo Administrador)

INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(7, 'actualizacion.masiva', 'Actualización masiva de equipos');

INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido) VALUES
('Administrador', 'actualizacion.masiva', 1),
('TecnicoIT',      'actualizacion.masiva', 0),
('Consulta',       'actualizacion.masiva', 0);
GO
