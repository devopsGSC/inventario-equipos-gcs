-- Nuevas acciones para gestión de tipos personalizados
-- Módulo Equipos (Id 1) y Periféricos (Id 2) ya existen
INSERT INTO AccionesModulo (ModuloId, Clave, Nombre) VALUES
(1, 'equipos.tipos.crear',    'Crear tipo de equipo personalizado'),
(1, 'equipos.tipos.eliminar', 'Eliminar tipo de equipo personalizado'),
(2, 'perifericos.tipos.crear',    'Crear tipo de periférico personalizado'),
(2, 'perifericos.tipos.eliminar', 'Eliminar tipo de periférico personalizado');

-- Permisos por defecto: Administrador y TecnicoIT pueden, Consulta no
INSERT INTO PermisosRol (RolNombre, AccionClave, Permitido) VALUES
('Administrador', 'equipos.tipos.crear',        1),
('Administrador', 'equipos.tipos.eliminar',     1),
('Administrador', 'perifericos.tipos.crear',    1),
('Administrador', 'perifericos.tipos.eliminar', 1),
('TecnicoIT',     'equipos.tipos.crear',        1),
('TecnicoIT',     'equipos.tipos.eliminar',     1),
('TecnicoIT',     'perifericos.tipos.crear',    1),
('TecnicoIT',     'perifericos.tipos.eliminar', 1),
('Consulta',      'equipos.tipos.crear',        0),
('Consulta',      'equipos.tipos.eliminar',     0),
('Consulta',      'perifericos.tipos.crear',    0),
('Consulta',      'perifericos.tipos.eliminar', 0);
GO
