# InventarioTI v2 — Global Customs Solutions

Sistema de gestión del ciclo de vida de activos tecnológicos.

## Requisitos
- .NET 8 SDK
- SQL Server (LocalDB, Express o completo)

## Instalación

### 1. Base de datos
Ejecutar `InventarioTI_v2.sql` en SQL Server Management Studio.

### 2. Cadena de conexión
Editar `appsettings.json`:

```json
// Windows Auth (LocalDB o dominio)
"Default": "Server=localhost;Database=InventarioTI;Trusted_Connection=True;TrustServerCertificate=True;"

// SQL Auth
"Default": "Server=localhost;Database=InventarioTI;User Id=sa;Password=TuPassword;TrustServerCertificate=True;"

// LocalDB
"Default": "Server=(localdb)\\mssqllocaldb;Database=InventarioTI;Trusted_Connection=True;"
```

### 3. Ejecutar
```bash
cd InventarioTI
dotnet restore
dotnet run
```
Abrir: `https://localhost:5001`

## Funcionalidades

### Inventario
- Registro de equipos con fecha de compra y garantía
- Estados: **Bodega → Asignado / Préstamo / En Garantía → Bodega → Baja**
- Búsqueda por número de serie desde el dashboard
- Alertas de garantía próxima a vencer (30 días)

### Movimientos (Kardex)
- Asignación permanente a empleado
- Préstamo temporal con fecha de devolución estimada
- Entrada/salida de garantía
- Devolución a bodega
- Historial completo con línea de tiempo por equipo

### Empleados
- Catálogo con código de empleado, DUI, cargo y departamento
- Vista de equipos actuales por empleado
- Activar / desactivar empleados

### Carta de compromiso PDF
- Se genera automáticamente al registrar asignación o préstamo
- Diferencia entre carta de asignación y carta de préstamo
- Datos pre-llenados: empleado, equipo, fecha, accesorios

## Dependencias NuGet
```
Microsoft.EntityFrameworkCore.SqlServer  8.0.0
Microsoft.EntityFrameworkCore.Tools      8.0.0
iTextSharp.LGPLv2.Core                  3.4.2
```
