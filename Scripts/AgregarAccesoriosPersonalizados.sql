-- =========================================================
-- AgregarAccesoriosPersonalizados.sql
-- Tabla para persistir accesorios de equipo (fijos + custom)
-- =========================================================

CREATE TABLE AccesoriosEquipo (
    Id     INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL,
    CONSTRAINT UQ_AccesoriosEquipo_Nombre UNIQUE (Nombre)
);

-- Seed: accesorios fijos que existían como hardcode
SET IDENTITY_INSERT AccesoriosEquipo ON;
INSERT INTO AccesoriosEquipo (Id, Nombre) VALUES (1, 'Cargador'), (2, 'Funda');
SET IDENTITY_INSERT AccesoriosEquipo OFF;
