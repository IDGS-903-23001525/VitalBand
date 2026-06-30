using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore; // ¡Añadido para MySQL!
using VitalBand.Data;               // ¡Añadido para jalar tu VitalBandContext!
using VitalBand.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar servicios de MVC
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IConfiguracionService, ConfiguracionService>();

// 2. 👇 FUSIÓN AQUÍ: Agregamos la conexión real a MySQL usando tu Contexto
builder.Services.AddDbContext<VitalBandContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("MySqlConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("MySqlConnection"))
    )
);

// 3. Agregar autenticación con cookies (Se queda exactamente igual a como lo tenían)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";      // Redirige aquí si no está autenticado
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });


var app = builder.Build();

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseMiddleware<VitalBand.Middleware.SessionTimeoutMiddleware>();

// ¡Importante! Deben ir en este orden
app.UseAuthentication();  // Primero autenticación
app.UseAuthorization();   // Luego autorización

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");  // Redirige a Login por defecto

app.Run();