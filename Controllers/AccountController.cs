using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
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

            // =========================================================================
            // NUEVA LÓGICA DE CONTROL DE SESIÓN ÚNICA POR INACTIVIDAD
            // =========================================================================
            string nuevoTokenSesion = Guid.NewGuid().ToString();

            usuario.token_sesion = nuevoTokenSesion;
            usuario.sesion_expiracion = DateTime.Now.AddMinutes(20); // Tiempo inicial: 20 min

            _context.Usuarios.Update(usuario);
            await _context.SaveChangesAsync();
            // =========================================================================

            string nombreMostrar = "Usuario VitalBand";
            int perfilId = 0;

            if (usuario.rol == "medico" || usuario.rol == "Medico")
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

        new(ClaimTypes.NameIdentifier, usuario.id.ToString()),
        new(ClaimTypes.Name, nombreMostrar),
        new(ClaimTypes.Email, usuario.email),

        new(ClaimTypes.Role, usuario.rol),
        new(ClaimTypes.Role, char.ToUpper(usuario.rol[0]) + usuario.rol.Substring(1)),

        new("UsuarioBaseId", usuario.id.ToString()),
        new("PerfilId", perfilId.ToString()),
        
        // Guardamos el token generado en la cookie de este navegador específico
        new("TokenSesionActual", nuevoTokenSesion)
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

            if (usuario.rol == "medico" || usuario.rol == "Medico")
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

            // Buscamos si el correo existe en la tabla USUARIOS
            var usuario = _context.Usuarios.FirstOrDefault(u => u.email == email);

            if (usuario != null)
            {
                // 1. Generamos un token único y seguro para la URL
                string tokenUrl = Guid.NewGuid().ToString();

                // 2. Guardamos el token y le damos 15 minutos de vida útil
                usuario.token_sesion = tokenUrl;
                usuario.sesion_expiracion = DateTime.Now.AddMinutes(15);

                _context.Usuarios.Update(usuario);
                await _context.SaveChangesAsync();

                // 3. Construimos el enlace dinámico que apunta a la vista ResetPassword
                var callbackUrl = Url.Action("ResetPassword", "Account", new { token = tokenUrl, email = email }, Request.Scheme);

                // 4. Mandamos el correo electrónico real
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

            // Por seguridad, mostramos el mismo mensaje siempre (así los atacantes no saben qué correos existen)
            TempData["SuccessMessage"] = "Se ha enviado un enlace de recuperación a tu correo. ¡Revisa tu bandeja de entrada y sigue las instrucciones!";
            return View("Index");
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            // Si entran a la mala sin token o sin correo, los patitas para la calle (al Login)
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login");
            }

            // Le inyectamos el Token y el Email al ViewModel para que la vista los tenga ocultos
            var model = new ResetPasswordViewModel
            {
                Token = token,
                Email = email
            };

            return View("RestablecerContrasenia" , model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Buscamos que coincida el email, el token y que la fecha de expiración siga siendo mayor a "ahora"
            var usuario = _context.Usuarios.FirstOrDefault(u =>
                u.email == model.Email &&
                u.token_sesion == model.Token &&
                u.sesion_expiracion > DateTime.Now);

            if (usuario == null)
            {
                ModelState.AddModelError(string.Empty, "El enlace de recuperación es inválido o ya expiró (límite de 15 minutos). ❌");
                return View(model);
            }

            // Guardamos la nueva contraseña (si manejas hash, pásala por tu función de encriptar aquí)
            usuario.password_hash = model.Password;

            // Limpiamos los campos para que ese token no se pueda volver a usar jamás
            usuario.token_sesion = null;
            usuario.sesion_expiracion = null;

            _context.Usuarios.Update(usuario);
            await _context.SaveChangesAsync();

            // Mandamos el veredicto positivo. La vista detectará esto, ocultará los inputs y activará los 15 segundos en JS.
            ViewBag.Exito = true;
            return View("RestablecerContrasenia");
        }

        // =========================================================================
        // MÉTODO AUXILIAR: ENVÍO DE CORREOS REALES MEDIANTE SMTP
        // =========================================================================
        private void EnviarCorreo(string destinatario, string asunto, string cuerpoHtml)
        {
            try
            {
                var correo = new MailMessage();
                correo.From = new MailAddress("vitalband65@gmail.com", "VitalBand Seguridad");
                correo.To.Add(destinatario);
                correo.Subject = asunto;
                correo.Body = cuerpoHtml;
                correo.IsBodyHtml = true; // Permite mandar el botón con diseño responsivo

                var smtp = new SmtpClient("smtp.gmail.com") // Cambia por tu servidor (ej. smtp.office365.com)
                {
                    Port = 587,
                    Credentials = new NetworkCredential("vitalband65@gmail.com", "pblm pxar lyrf lfkz"),
                    EnableSsl = true
                };

                smtp.Send(correo);
            }
            catch (Exception ex)
            {
                // Registra el error en consola por si falla la contraseña del SMTP durante tus pruebas
                Console.WriteLine($"Error al enviar correo: {ex.Message}");
            }
        }

        // GET: Account/RegistroMedico
        [HttpGet]
        public IActionResult RegistroMedico()
        {
            return View();
        }

        // POST: Account/RegistroMedico
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistroMedico(string nombre, string specialty, string cedula, string email, string password)
        {
            // 1. Validar que no vengan vacíos
            if (string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(specialty) ||
                string.IsNullOrEmpty(cedula) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Todos los campos son obligatorios. ⚠️";
                return View();
            }

            // 2. Validar si el correo ya existe
            var emailExiste = _context.Usuarios.Any(u => u.email == email);
            if (emailExiste)
            {
                ViewBag.Error = "Este correo electrónico ya está registrado en VitalBand 🔒";
                return View();
            }

            // 3. Validar si la cédula ya existe
            var cedulaExiste = _context.Medicos.Any(m => m.cedula_profesional == cedula);
            if (cedulaExiste)
            {
                ViewBag.Error = "Esta cédula profesional ya está registrada 📜";
                return View();
            }

            // 4. Guardar en la Base de Datos usando una transacción relacional
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var nuevoUsuario = new Usuario
                    {
                        email = email,
                        password_hash = password, // Tu lógica actual en texto plano
                        rol = "medico",
                        fecha_registro = System.DateTime.Now
                    };

                    _context.Usuarios.Add(nuevoUsuario);
                    await _context.SaveChangesAsync(); // Genera el id del usuario

                    var nuevoMedico = new Medico
                    {
                        usuario_id = nuevoUsuario.id, // Ligamos la FK
                        nombre = nombre,
                        especialidad = specialty,
                        cedula_profesional = cedula
                    };

                    _context.Medicos.Add(nuevoMedico);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["SuccessRegister"] = $"¡Registro exitoso, Dr. {nombre}! Ya puede iniciar sesión. 🩺✨";
                    return RedirectToAction(nameof(Login));
                }
                catch (System.Exception)
                {
                    await transaction.RollbackAsync();
                    ViewBag.Error = "Ocurrió un error interno al registrar. Intente más tarde.";
                    return View();
                }
            }

            return View();
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
                    var usuario = _context.Usuarios.FirstOrDefault(u => u.id == userId);
                    if (usuario != null)
                    {
                        // Limpiamos los campos en MySQL al cerrar sesión manualmente
                        usuario.token_sesion = null;
                        usuario.sesion_expiracion = null;
                        _context.Usuarios.Update(usuario);
                        await _context.SaveChangesAsync();
                    }
                }
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }
    }
}