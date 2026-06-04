using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<PdfService>();

var app = builder.Build();

// Mostrar errores detallados siempre (quitar en producción)
app.UseDeveloperExceptionPage();

app.UseStaticFiles();
app.UseRouting();
app.MapDefaultControllerRoute();
app.Run();
