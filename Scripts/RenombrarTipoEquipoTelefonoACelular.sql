-- =========================================================
-- RenombrarTipoEquipoTelefonoACelular.sql
-- Renombra el catálogo TiposEquipo de "Teléfono" a "Celular"
-- para reflejar el nuevo nombre por defecto usado en la app.
-- =========================================================

UPDATE TiposEquipo SET Nombre = 'Celular' WHERE Nombre = 'Teléfono';
GO
