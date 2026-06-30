using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    public class AccountController : Controller
    {
        // Cambiamos el contexto de BD por el cliente HTTP
        private readonly IHttpClientFactory _clientFactory;

        public AccountController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
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

            // 1. Preparamos el cliente HTTP para llamar a nuestra API
            var client = _clientFactory.CreateClient();

            // ⚠️ Recuerda verificar el puerto exacto de tu localhost local
            string urlApi = "https://localhost:7116/api/UsuariosApi/login";

            // 2. Enviamos los datos del formulario en formato JSON hacia la API
            var response = await client.PostAsJsonAsync(urlApi, model);

            // 3. Si las credenciales son incorrectas (HTTP 401 Unauthorized u otros errores)
            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "Correo electrónico o contraseña incorrectos 🔒❤️");
                return View(model);
            }

            // 4. Si salió bien, leemos la respuesta con los datos procesados por la API
            var datosLogin = await response.Content.ReadFromJsonAsync<LoginResponseDTO>();

            if (datosLogin == null)
            {
                ModelState.AddModelError(string.Empty, "Hubo un problema al procesar el inicio de sesión.");
                return View(model);
            }

            // 5. Armamos los Claims usando lo que nos dio la API
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, datosLogin.NombreCompleto),
                new(ClaimTypes.Email, datosLogin.Email),

                new(ClaimTypes.Role, datosLogin.Rol),
                new(ClaimTypes.Role, char.ToUpper(datosLogin.Rol[0]) + datosLogin.Rol.Substring(1)),

                new("UsuarioBaseId", datosLogin.Id.ToString()),
                new("PerfilId", datosLogin.PerfilId.ToString())
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

            if (datosLogin.Rol == "medico")
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

    // Objeto temporal (DTO) para mapear lo que devuelve la API de Login de forma segura
    public class LoginResponseDTO
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Rol { get; set; }
        public string NombreCompleto { get; set; }
        public int PerfilId { get; set; }
    }
}