-- =========================================================
-- ResetPasswordAdminLocal.sql
-- Resetea la contraseña de admin@gcs.com.sv a "Admin2024!" en tu base
-- LOCAL de pruebas (el hash fue generado con el mismo PasswordHasher
-- que usa la app, así que el login funciona igual que si la hubieras
-- cambiado desde la pantalla de la app).
-- También limpia los intentos fallidos para que no quede bloqueada.
--
-- Solo para uso local/pruebas — no correr contra producción.
-- =========================================================

UPDATE AspNetUsers
SET PasswordHash      = 'AQAAAAIAAYagAAAAEJ6b0bVw4MS+gu7XVUNUxASmO3Vm8PhmO9Wrb11H4iWpgITKG4VxAomX3zWiz/WUPA==',
    AccessFailedCount = 0,
    LockoutEnd        = NULL
WHERE Email = 'admin@gcs.com.sv';
