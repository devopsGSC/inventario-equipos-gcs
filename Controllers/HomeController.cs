using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.ViewModels;

namespace InventarioTI.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? serie)
    {
        var vm = new DashboardViewModel
        {
            TotalEquipos    = await _db.Equipos.CountAsync(),
            EnBodega        = await _db.Equipos.CountAsync(e => e.Estado == "Bodega"),
            Asignados       = await _db.Equipos.CountAsync(e => e.Estado == "Asignado"),
            EnPrestamo      = await _db.Equipos.CountAsync(e => e.Estado == "Prestamo"),
            EnGarantia      = await _db.Equipos.CountAsync(e => e.Estado == "EnGarantia"),
            GarantiasProximas = await _db.Equipos.CountAsync(e =>
                e.FechaGarantia.HasValue &&
                e.FechaGarantia.Value >= DateTime.Today &&
                e.FechaGarantia.Value <= DateTime.Today.AddDays(30)),
            UltimosRegistros = await _db.Equipos
                .Include(e => e.TipoEquipo)
                .OrderByDescending(e => e.FechaRegistro)
                .Take(5).ToListAsync(),
            UltimosMovimientos = await _db.Movimientos
                .Include(m => m.Equipo)
                .Include(m => m.Empleado)
                .OrderByDescending(m => m.FechaInicio)
                .Take(6).ToListAsync()
        };

        if (!string.IsNullOrWhiteSpace(serie))
        {
            vm.SerieBuscada = serie;
            vm.ResultadoBusqueda = await _db.Equipos
                .Include(e => e.TipoEquipo)
                .Include(e => e.Movimientos.Where(m => m.FechaDevolucion == null &&
                    (m.TipoMovimiento == "Asignacion" || m.TipoMovimiento == "Prestamo" || m.TipoMovimiento == "EntradaGarantia")))
                    .ThenInclude(m => m.Empleado).ThenInclude(emp => emp!.Departamento)
                .FirstOrDefaultAsync(e => e.NumeroSerie == serie);
        }

        return View(vm);
    }
}
