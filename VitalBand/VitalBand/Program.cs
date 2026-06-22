using Microsoft.AspNetCore.Authentication.Cookies;
using VitalBand.Services;

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios de MVC
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IConfiguracionService, ConfiguracionService>();

// Agregar autenticación con cookies
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

// ¡Importante! Deben ir en este orden
app.UseAuthentication();  // Primero autenticación
app.UseAuthorization();   // Luego autorización

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");  // Redirige a Login por defecto


app.Run();