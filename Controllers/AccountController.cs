using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VitalBand.Models;
using VitalBand.Services;
using VitalBand.DTOs;

namespace VitalBand.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IApiUrlProvider _apiUrlProvider;

        public AccountController(IHttpClientFactory clientFactory, IApiUrlProvider apiUrlProvider)
        {
            _clientFactory = clientFactory;
            _apiUrlProvider = apiUrlProvider;
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
            var response = await client.PostAsJsonAsync(_apiUrlProvider.GetApiUrl("/api/UsuariosApi/login"), model);

            if (!response.IsSuccessStatusCode)
            {
                // Se conserva tu emoji personalizado para el error
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
                new("TokenSesionActual", datosLogin.TokenSesionActual)
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
            var response = await client.PostAsJsonAsync(_apiUrlProvider.GetApiUrl("/api/UsuariosApi/forgot-password"), email);

            if (response.IsSuccessStatusCode)
            {
                var resultado = await response.Content.ReadFromJsonAsync<TokenResponseDTO>();
                var callbackUrl = Url.Action("ResetPassword", "Account", new { token = resultado?.Token, email = email }, Request.Scheme);

                string mensajeBody = $@"
                <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 12px;'>
                    <h2 style='color: #0f766e;'>Restablecer Contraseña - VitalBand</h2>
                    <p>Recibimos una solicitud para cambiar la contraseña de tu cuenta.</p>
                    <p>Para continuar, haz clic en el siguiente botón (este enlace expirará en 15 minutos):</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{callbackUrl}' style='background: linear-gradient(135deg, #0f766e, #0d9488); color: white; padding: 12px 24px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>Restablecer Contraseña</a>
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
            var response = await client.PostAsJsonAsync(_apiUrlProvider.GetApiUrl("/api/UsuariosApi/reset-password"), model);

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

            var response = await client.PostAsJsonAsync(_apiUrlProvider.GetApiUrl("/api/UsuariosApi/registro-medico"), dto);

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = await response.Content.ReadAsStringAsync() ?? "Error interno al registrar. Intente más tarde.";
                return View();
            }

            TempData["SuccessRegister"] = $"¡Registro exitoso, Dr. {nombre}! Ya puede iniciar sesión. 🩺✨";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult RegistroPaciente()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistroPaciente(RegistroPacienteDTO model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Por favor, verifica que los datos ingresados sean correctos.";
                return View(model);
            }

            var client = _clientFactory.CreateClient();
            var jsonString = JsonSerializer.Serialize(model);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(_apiUrlProvider.GetApiUrl("/api/ConfiguracionApi/registro-paciente"), content);

            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = await response.Content.ReadAsStringAsync() ?? "Error interno al procesar el Onboarding clínico.";
                return View(model);
            }

            TempData["SuccessRegister"] = "¡Tu Onboarding clínico se ha completado con éxito! Ya puedes iniciar sesión.";
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
                    await client.PostAsync(_apiUrlProvider.GetApiUrl($"/api/UsuariosApi/logout/{userId}"), null);
                }
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Redirige correctamente a la Landing Page corporativa (Home/Index)
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UploadAvatar(IFormFile avatar)
        {
            var claimId = User.FindFirst("UsuarioBaseId")?.Value;
            if (!int.TryParse(claimId, out int userId)) return Unauthorized();

            if (avatar == null || avatar.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "pacientes");
            Directory.CreateDirectory(uploads);

            var ext = Path.GetExtension(avatar.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".png";

            var fileName = userId + ext;
            var filePath = Path.Combine(uploads, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await avatar.CopyToAsync(stream);
            }

            var url = $"/img/pacientes/{fileName}";
            return Ok(new { url });
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
        public string Email { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public int PerfilId { get; set; }
        public string TokenSesionActual { get; set; } = string.Empty;
    }

    public class TokenResponseDTO { public string Token { get; set; } = string.Empty; }
    public class RegistroMedicoDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Cedula { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}