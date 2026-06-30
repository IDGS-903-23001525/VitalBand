using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private const string BaseUrl = "https://localhost:7116/api/UsuariosApi"; // ⚠️ Verifica tu puerto local

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

            var client = _clientFactory.CreateClient();
            var response = await client.PostAsJsonAsync($"{BaseUrl}/login", model);

            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "Correo electrónico o contraseña incorrectos 🔒❤️");
                return View(model);
            }

            var datosLogin = await response.Content.ReadFromJsonAsync<LoginResponseDTO>();
            if (datosLogin == null)
            {
                ModelState.AddModelError(string.Empty, "Hubo un problema al procesar el inicio de sesión.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, datosLogin.Id.ToString()),
                new(ClaimTypes.Name, datosLogin.NombreCompleto),
                new(ClaimTypes.Email, datosLogin.Email),
                new(ClaimTypes.Role, datosLogin.Rol),
                new(ClaimTypes.Role, char.ToUpper(datosLogin.Rol[0]) + datosLogin.Rol.Substring(1)),
                new("UsuarioBaseId", datosLogin.Id.ToString()),
                new("PerfilId", datosLogin.PerfilId.ToString()),
                new("TokenSesionActual", datosLogin.TokenSesionActual) // Sesión por inactividad unificada
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20)
            });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (datosLogin.Rol.ToLower() == "medico")
                return RedirectToAction("Index", "Usuarios");
            else
                return RedirectToAction("MiHistorial", "Historial");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError(string.Empty, "Por favor, ingresa tu correo electrónico.");
                return View();
            }

            var client = _clientFactory.CreateClient();
            var response = await client.PostAsJsonAsync($"{BaseUrl}/forgot-password", email);

            if (response.IsSuccessStatusCode)
            {
                var resultado = await response.Content.ReadFromJsonAsync<TokenResponseDTO>();
                var callbackUrl = Url.Action("ResetPassword", "Account", new { token = resultado?.Token, email = email }, Request.Scheme);

                string mensajeBody = $@"
                <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 12px;'>
                    <h2 style='color: #0a6775;'>Restablecer Contraseña - VitalBand</h2>
                    <p>Recibimos una solicitud para cambiar la contraseña de tu cuenta.</p>
                    <p>Para continuar, haz clic en el siguiente botón (este enlace expirará en 15 minutos):</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{callbackUrl}' style='background: linear-gradient(135deg, #0a6775, #06b6d4); color: white; padding: 12px 24px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>Restablecer Contraseña</a>
                    </div>
                    <p style='color: #64748b; font-size: 0.85em;'>Si tú no solicitaste este cambio, puedes ignorar este correo de forma segura.</p>
                </div>";

                EnviarCorreo(email, "Restablece tu contraseña de VitalBand 🔑", mensajeBody);
            }

            TempData["SuccessMessage"] = "Se ha enviado un enlace de recuperación a tu correo. ¡Revisa tu bandeja de entrada y sigue las instrucciones!";
            return View("Index");
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email)) return RedirectToAction("Login");

            var model = new ResetPasswordViewModel { Token = token, Email = email };
            return View("RestablecerContrasenia", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var client = _clientFactory.CreateClient();
            var response = await client.PostAsJsonAsync($"{BaseUrl}/reset-password", model);

            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "El enlace de recuperación es inválido o ya expiró (límite de 15 minutos). ❌");
                return View(model);
            }

            ViewBag.Exito = true;
            return View("RestablecerContrasenia");
        }

        [HttpGet]
        public IActionResult RegistroMedico() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistroMedico(string nombre, string specialty, string cedula, string email, string password)
        {
            if (string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(specialty) || string.IsNullOrEmpty(cedula) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Todos los campos son obligatorios. ⚠️";
                return View();
            }

            var client = _clientFactory.CreateClient();
            var dto = new RegistroMedicoDTO { Nombre = nombre, Specialty = specialty, Cedula = cedula, Email = email, Password = password };

            var response = await client.PostAsJsonAsync($"{BaseUrl}/registro-medico", dto);

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = await response.Content.ReadAsStringAsync() ?? "Error interno al registrar. Intente más tarde.";
                return View();
            }

            TempData["SuccessRegister"] = $"¡Registro exitoso, Dr. {nombre}! Ya puede iniciar sesión. 🩺✨";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var claimId = User.FindFirst("UsuarioBaseId")?.Value;
                if (int.TryParse(claimId, out int userId))
                {
                    var client = _clientFactory.CreateClient();
                    await client.PostAsync($"{BaseUrl}/logout/{userId}", null); // Borra tokens en BD mediante API
                }
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        private void EnviarCorreo(string destinatario, string asunto, string cuerpoHtml)
        {
            try
            {
                var correo = new MailMessage();
                correo.From = new MailAddress("vitalband65@gmail.com", "VitalBand Seguridad");
                correo.To.Add(destinatario);
                correo.Subject = asunto;
                correo.Body = cuerpoHtml;
                correo.IsBodyHtml = true;

                var smtp = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("vitalband65@gmail.com", "pblm pxar lyrf lfkz"),
                    EnableSsl = true
                };
                smtp.Send(correo);
            }
            catch (Exception ex) { Console.WriteLine($"Error al enviar correo: {ex.Message}"); }
        }
    }

    public class LoginResponseDTO
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Rol { get; set; }
        public string NombreCompleto { get; set; }
        public int PerfilId { get; set; }
        public string TokenSesionActual { get; set; }
    }

    public class TokenResponseDTO { public string Token { get; set; } }
    public class RegistroMedicoDTO
    {
        public string Nombre { get; set; }
        public string Specialty { get; set; }
        public string Cedula { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
}