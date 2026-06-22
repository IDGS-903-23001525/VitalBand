using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            string rol = "";
            int? usuarioId = null;

            // Validación de credenciales (ejemplo)
            if (model.Email == "medico@vitalband.com" && model.Password == "VitalBand2026!")
            {
                rol = "Medico";
                usuarioId = 0; // médico no tiene ID de paciente
            }
            else if (model.Email == "paciente@vitalband.com" && model.Password == "VitalBand2026!")
            {
                rol = "Paciente";
                // Obtener ID real del paciente desde la lista de usuarios
                var usuarios = UsuariosController.ObtenerUsuarios();
                var usuario = usuarios.FirstOrDefault(u => u.Email == model.Email);
                usuarioId = usuario?.Id ?? 2; // fallback a Carlos (id 2)
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Credenciales inválidas");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, model.Email),
                new(ClaimTypes.Email, model.Email),
                new(ClaimTypes.Role, rol),
                new("UsuarioId", usuarioId?.ToString() ?? "")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (rol == "Medico")
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