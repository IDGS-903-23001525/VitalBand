using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System;
using System.Linq;
using System.Threading.Tasks;
using VitalBand.Data;

namespace VitalBand.Middleware
{
    public class SessionTimeoutMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionTimeoutMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, VitalBandContext dbContext)
        {
            if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                var userIdClaim = context.User.FindFirst("UsuarioBaseId")?.Value;
                var tokenClaim = context.User.FindFirst("TokenSesionActual")?.Value;

                if (int.TryParse(userIdClaim, out int userId) && !string.IsNullOrEmpty(tokenClaim))
                {
                    var usuario = dbContext.Usuarios.FirstOrDefault(u => u.id == userId);

                    if (usuario != null)
                    {
                        // CASO 1: Inicio de sesión en otro dispositivo (Tokens diferentes)
                        if (usuario.token_sesion != tokenClaim)
                        {
                            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            context.Response.Redirect("/Account/Login");
                            return;
                        }

                        // CASO 2: Expiró por inactividad real de 20 minutos
                        if (usuario.sesion_expiracion.HasValue && DateTime.Now > usuario.sesion_expiracion.Value)
                        {
                            usuario.token_sesion = null;
                            usuario.sesion_expiracion = null;
                            dbContext.Usuarios.Update(usuario);
                            await dbContext.SaveChangesAsync();

                            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            context.Response.Redirect("/Account/Login?returnUrl=" + context.Request.Path);
                            return;
                        }

                        // CASO 3: El usuario está activo navegando. Renovamos su tiempo por 20 min más.
                        usuario.sesion_expiracion = DateTime.Now.AddMinutes(20);
                        dbContext.Usuarios.Update(usuario);
                        await dbContext.SaveChangesAsync();
                    }
                }
            }

            await _next(context);
        }
    }
}