-- =========================================================
-- EliminarAsignacionViaEquipo.sql
-- La app dejó de soportar la asignación de periféricos/licencias
-- "vía equipo" (atados a un Equipo, viajando con él si cambia de
-- responsable). De ahora en adelante TODO se asigna "Directo" a una
-- persona/grupo, nunca a un equipo.
--
-- Este script migra los registros existentes con TipoAsignacion =
-- 'Equipo' a 'Directo', quitándoles el EquipoId — quedan como si
-- siempre hubieran sido asignados directo a la persona/grupo que ya
-- tenían guardado en EmpleadoId/MiembroExternoId/GrupoId (ese dato
-- ya se llenaba también en la asignación vía equipo, así que no se
-- pierde nada).
--
-- Idempotente: se puede correr mas de una vez sin problema (la
-- segunda vez ya no hay filas con TipoAsignacion = 'Equipo').
-- =========================================================

UPDATE EquiposPerifericos
SET TipoAsignacion = 'Directo', EquipoId = NULL
WHERE TipoAsignacion = 'Equipo';
GO

UPDATE LicenciasAsignaciones
SET TipoAsignacion = 'Directo', EquipoId = NULL
WHERE TipoAsignacion = 'Equipo';
GO
