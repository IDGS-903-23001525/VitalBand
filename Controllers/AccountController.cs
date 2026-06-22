using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using VitalBand.Data;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    public class AccountController : Controller
    {
        private readonly VitalBandContext _context;

        // Inyectamos el contexto de VitalBand para conectarnos a MySQL
        public AccountController(VitalBandContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Si ya está autenticado, lo redirigimos según su rol para que no vuelva a ver el Login
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                // Buscamos cualquier variante para redirigir correctamente
                if (User.IsInRole("medico") || User.IsInRole("Medico"))
                    return RedirectToAction("Index", "Usuarios");
                else
                    return RedirectToAction("MiHistorial", "Historial");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            // 1. Buscamos al usuario en la tabla base 'USUARIOS'
            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.email == model.Email && u.password_hash == model.Password);

            if (usuario == null)
            {
                // Si no existe o la contraseña está mal, disparamos el error de validación automático
                ModelState.AddModelError(string.Empty, "Correo electrónico o contraseña incorrectos 🔒❤️");
                return View(model);
            }

            // Variables para almacenar la información específica del perfil
            string nombreMostrar = "Usuario VitalBand";
            int perfilId = 0;

            // 2. Buscamos el perfil real en MEDICOS o PACIENTES según el rol de la BD
            if (usuario.rol == "medico")
            {
                var medico = _context.Medicos.FirstOrDefault(m => m.usuario_id == usuario.id);
                if (medico != null)
                {
                    nombreMostrar = medico.nombre;
                    perfilId = medico.id; // ID primario de la tabla MEDICOS
                }
            }
            else if (usuario.rol == "paciente")
            {
                var paciente = _context.Pacientes.FirstOrDefault(p => p.usuario_id == usuario.id);
                if (paciente != null)
                {
                    nombreMostrar = paciente.nombre;
                    perfilId = paciente.id; // ID primario de la tabla PACIENTES
                }
            }

            // 3. Creamos los Claims (Insignias de identidad cifradas en la Cookie)
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, nombreMostrar), // El nombre real que saldrá en la barra superior
                new(ClaimTypes.Email, usuario.email),
                
                // --- 🛠️ SOLUCIÓN AL ACCESS DENIED: Enviamos tanto minúscula como formato Capitalizado ---
                new(ClaimTypes.Role, usuario.rol),                                 // 'medico' o 'paciente' (Mapea con la BD)
                new(ClaimTypes.Role, char.ToUpper(usuario.rol[0]) + usuario.rol.Substring(1)), // 'Medico' o 'Paciente' (Mapea con .NET)
                // -------------------------------------------------------------------------------------
                
                new("UsuarioBaseId", usuario.id.ToString()),
                new("PerfilId", perfilId.ToString())   // ID útil para jalar las ALERTAS o el HISTORIAL correspondiente
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // 4. Guardamos la Cookie de autenticación en el navegador
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = model.RememberMe, // Si marcó "Mantener sesión iniciada", la cookie no se borra al cerrar el navegador
                ExpiresUtc = System.DateTimeOffset.UtcNow.AddMinutes(20) // Caduca en 20 minutos
            });

            // 5. Redireccionamiento inteligente
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (usuario.rol == "medico")
                return RedirectToAction("Index", "Usuarios");
            else
                return RedirectToAction("MiHistorial", "Historial");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }
    }
}