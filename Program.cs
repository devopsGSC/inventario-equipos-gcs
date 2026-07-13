using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
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

var dbServer = Environment.GetEnvironmentVariable("DB_SERVER");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

var connectionString =
    !string.IsNullOrEmpty(dbServer) && !string.IsNullOrEmpty(dbName)
        ? $"Server={dbServer};Database={dbName};User Id={dbUser};Password={dbPassword};TrustServerCertificate=True;"
        : builder.Configuration.GetConnectionString("Default");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(connectionString));
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

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Apache corre en el mismo servidor (proxy reverso local); se limpian
    // KnownNetworks/KnownProxies porque por defecto solo se confía en loopback
    // exacto y eso a veces no coincide con la IP que reporta Apache.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Configuration["Email:From"] =
    Environment.GetEnvironmentVariable("EMAIL_FROM");

builder.Configuration["Email:AppPassword"] =
    Environment.GetEnvironmentVariable("EMAIL_APP_PASSWORD");

builder.Configuration["Email:SmtpHost"] =
    Environment.GetEnvironmentVariable("EMAIL_SMTP_HOST");

builder.Configuration["Email:SmtpPort"] =
    Environment.GetEnvironmentVariable("EMAIL_SMTP_PORT");

var app = builder.Build();

// Ejecutar seed al arrancar
using (var scope = app.Services.CreateScope())
{
    var seed = scope.ServiceProvider.GetRequiredService<SeedService>();
    await seed.InicializarAsync();
}

// Mostrar errores detallados siempre (quitar en producción)
app.UseDeveloperExceptionPage();

// Debe ir antes que cualquier middleware que dependa del esquema/host
// (redirects, cookies, generación de URLs), para que ASP.NET Core sepa
// que la petición original llegó por HTTPS aunque Apache la reenvíe por HTTP.
app.UseForwardedHeaders();

app.UseRequestLocalization();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultControllerRoute();
app.Run();
