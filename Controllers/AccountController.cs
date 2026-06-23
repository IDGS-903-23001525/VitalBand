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

        public AccountController(VitalBandContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("medico") || User.IsInRole("Medico"))
                    return RedirectToAction("Index", "Usuarios");
                else
                    return RedirectToAction("MiHistorial", "Historial");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(Login model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.email == model.Email && u.password_hash == model.Password);

            if (usuario == null)
            {
                ModelState.AddModelError(string.Empty, "Correo electrónico o contraseña incorrectos 🔒❤️");
                return View(model);
            }

            string nombreMostrar = "Usuario VitalBand";
            int perfilId = 0;

            if (usuario.rol == "medico")
            {
                var medico = _context.Medicos.FirstOrDefault(m => m.usuario_id == usuario.id);
                if (medico != null)
                {
                    nombreMostrar = medico.nombre;
                    perfilId = medico.id;
                }
            }
            else if (usuario.rol == "paciente")
            {
                var paciente = _context.Pacientes.FirstOrDefault(p => p.usuario_id == usuario.id);
                if (paciente != null)
                {
                    nombreMostrar = paciente.nombre;
                    perfilId = paciente.id;
                }
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, nombreMostrar),
                new(ClaimTypes.Email, usuario.email),
                
                new(ClaimTypes.Role, usuario.rol),                                 
                new(ClaimTypes.Role, char.ToUpper(usuario.rol[0]) + usuario.rol.Substring(1)),
                
                new("UsuarioBaseId", usuario.id.ToString()),
                new("PerfilId", perfilId.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = System.DateTimeOffset.UtcNow.AddMinutes(20)
            });

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