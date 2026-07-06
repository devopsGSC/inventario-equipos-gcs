-- =========================================================
-- AgregarAsignacionDirectaPeriferico.sql
-- Permite asignar periféricos directamente a empleados,
-- sin necesidad de vincularlos a un equipo.
-- =========================================================

-- 1. Nuevas columnas
ALTER TABLE EquiposPerifericos ADD EmpleadoId     INT           NULL;
ALTER TABLE EquiposPerifericos ADD TipoAsignacion NVARCHAR(20)  NOT NULL DEFAULT 'Equipo';
ALTER TABLE EquiposPerifericos ADD FirmaEmpleado  NVARCHAR(MAX) NULL;
ALTER TABLE EquiposPerifericos ADD Observaciones  NVARCHAR(500) NULL;

-- 2. EquipoId pasa a nullable (primero drop FK, alter, re-add FK)
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_EquiposPerifericos_Equipos_EquipoId'
)
    ALTER TABLE EquiposPerifericos
        DROP CONSTRAINT FK_EquiposPerifericos_Equipos_EquipoId;

ALTER TABLE EquiposPerifericos ALTER COLUMN EquipoId INT NULL;

ALTER TABLE EquiposPerifericos
    ADD CONSTRAINT FK_EquiposPerifericos_Equipos_EquipoId
    FOREIGN KEY (EquipoId) REFERENCES Equipos(Id);

-- 3. FK a Empleados
ALTER TABLE EquiposPerifericos
    ADD CONSTRAINT FK_EquiposPerifericos_Empleados_EmpleadoId
    FOREIGN KEY (EmpleadoId) REFERENCES Empleados(Id);

-- 4. CHECK constraint
ALTER TABLE EquiposPerifericos
    ADD CONSTRAINT CK_EquiposPerifericos_TipoAsignacion
    CHECK (TipoAsignacion IN ('Equipo', 'Directo'));
