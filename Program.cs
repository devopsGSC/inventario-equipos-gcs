using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using InventarioTI.Data;
using InventarioTI.Models;
using InventarioTI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options =>
    {
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
        options.Filters.Add(new AuthorizeFilter(policy));
    })
    .AddDataAnnotationsLocalization()
    .AddMvcOptions(options =>
    {
        options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(
            _ => "El campo es obligatorio.");
        options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor(
            (value, field) => $"El valor '{value}' no es válido para {field}.");
        options.ModelBindingMessageProvider.SetMissingRequestBodyRequiredValueAccessor(
            () => "Se requiere un cuerpo en la solicitud.");
        options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(
            value => $"El valor '{value}' no es válido.");
        options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(
            field => $"El campo {field} debe ser un número.");
        options.ModelBindingMessageProvider.SetMissingKeyOrValueAccessor(
            () => "Se requiere un valor.");
    });

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("es-ES") };
    options.DefaultRequestCulture = new RequestCulture("es-ES");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<PermisoService>();

builder.Services.AddIdentity<UsuarioApp, IdentityRole>(options =>
{
    options.Password.RequireDigit           = true;
    options.Password.RequiredLength         = 8;
    options.Password.RequireUppercase       = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireLowercase       = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail         = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath        = "/Auth/Login";
    options.AccessDeniedPath = "/Auth/AccesoDenegado";
    options.ExpireTimeSpan   = TimeSpan.FromHours(8);
    options.SlidingExpiration= true;
});

builder.Services.AddScoped<SeedService>();

var app = builder.Build();

// Ejecutar seed al arrancar
using (var scope = app.Services.CreateScope())
{
    var seed = scope.ServiceProvider.GetRequiredService<SeedService>();
    await seed.InicializarAsync();
}

// Mostrar errores detallados siempre (quitar en producción)
app.UseDeveloperExceptionPage();

app.UseRequestLocalization();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultControllerRoute();
app.Run();
