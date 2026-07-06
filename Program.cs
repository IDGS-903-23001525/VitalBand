using System.Net.WebSockets;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore; 
using VitalBand.Data;               
using VitalBand.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar servicios de MVC
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IConfiguracionService, ConfiguracionService>();
builder.Services.AddSingleton<IApiUrlProvider, ApiUrlProvider>();

// 2. Configuración de base de datos MySQL por entorno
var connectionString = builder.Environment.IsProduction()
    ? builder.Configuration["ConnectionStrings:ProductionMySqlConnection"]
    : builder.Configuration.GetConnectionString("MySqlConnection");

builder.Services.AddDbContext<VitalBandContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)
    )
);

// 3. Agregar autenticación con cookies (Se queda exactamente igual a como lo tenían)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddHttpClient();

var app = builder.Build();

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});

app.MapGet("/api/alertas/{pacienteId:int?}", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var pacienteIdValue = context.Request.Query["pacienteId"].FirstOrDefault()
        ?? context.Request.Query["patientId"].FirstOrDefault()
        ?? context.Request.RouteValues["pacienteId"]?.ToString();

    if (!int.TryParse(pacienteIdValue, out var pacienteId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    VitalBand.Middleware.WebSocketConnectionManager.AddSocket(pacienteId, webSocket);

    var buffer = new byte[1024 * 4];

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Conexión cerrada", CancellationToken.None);
                break;
            }
        }
    }
    catch (OperationCanceledException)
    {
    }
    finally
    {
        await VitalBand.Middleware.WebSocketConnectionManager.RemoveSocket(pacienteId);
    }
});

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