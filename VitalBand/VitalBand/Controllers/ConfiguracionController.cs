using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VitalBand.Models;
using VitalBand.Services;

namespace VitalBand.Controllers
{
    [Authorize]
    public class ConfiguracionController : Controller
    {
        private readonly IConfiguracionService _config;

        public ConfiguracionController(IConfiguracionService config)
        {
            _config = config;
        }

        // Vista principal según rol
        public IActionResult Index()
        {
            if (User.IsInRole("Medico"))
            {
                var vm = new ConfiguracionViewModel
                {
                    RangosPulso = _config.ObtenerRangos(),
                    TiposAlerta = _config.ObtenerTiposAlerta()
                };
                return View("Index", vm);
            }
            else if (User.IsInRole("Paciente"))
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                var usuario = UsuariosController.ObtenerUsuarios().FirstOrDefault(u => u.Email == email);
                if (usuario == null) return NotFound();
                return View("Perfil", usuario);
            }
            return Forbid();
        }

        // Admin: agregar rango de pulso (solo médico)
        [HttpPost]
        [Authorize(Roles = "Medico")]
        public IActionResult AgregarRango(RangoPulsoConfig nuevoRango)
        {
            if (ModelState.IsValid)
                _config.AgregarRango(nuevoRango);
            return RedirectToAction(nameof(Index));
        }

        // Admin: agregar tipo de alerta (solo médico)
        [HttpPost]
        [Authorize(Roles = "Medico")]
        public IActionResult AgregarTipoAlerta(TipoAlertaConfig nuevoTipo)
        {
            if (ModelState.IsValid)
                _config.AgregarTipoAlerta(nuevoTipo);
            return RedirectToAction(nameof(Index));
        }

        // Paciente: actualizar perfil
        [HttpPost]
        [Authorize(Roles = "Paciente")]
        public IActionResult ActualizarPerfil(UsuarioResumenViewModel model)
        {
            if (ModelState.IsValid)
            {
                // En una app real, actualizarías la BD
                var usuarios = UsuariosController.ObtenerUsuarios();
                var usuario = usuarios.FirstOrDefault(u => u.Id == model.Id);
                if (usuario != null)
                {
                    usuario.Nombre = model.Nombre;
                    usuario.Edad = model.Edad;
                    usuario.Sexo = model.Sexo;
                    usuario.Email = model.Email;
                }
                TempData["Mensaje"] = "Perfil actualizado correctamente";
            }
            else
            {
                TempData["Error"] = "Error en los datos ingresados";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}