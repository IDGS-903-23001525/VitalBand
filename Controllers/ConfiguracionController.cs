using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using VitalBand.Data;
using VitalBand.Models;
using VitalBand.Services;

namespace VitalBand.Controllers
{
    [Authorize]
    public class ConfiguracionController : Controller
    {
        private readonly IConfiguracionService _config;
        private readonly VitalBandContext _context;

        public ConfiguracionController(IConfiguracionService config, VitalBandContext context)
        {
            _config = config;
            _context = context;
        }

        // GET: Configuracion
        public IActionResult Index()
        {
            if (User.IsInRole("Medico") || User.IsInRole("medico"))
            {
                var vm = new ConfiguracionViewModel
                {
                    RangosPulso = _config.ObtenerRangosPulso() ?? new List<RangoPulsoConfig>(),
                    TiposAlerta = _config.ObtenerTiposAlerta() ?? new List<TipoAlertaConfig>()
                };
                return View("Index", vm);
            }
            else if (User.IsInRole("Paciente") || User.IsInRole("paciente"))
            {
                var perfilIdStr = User.FindFirst("PerfilId")?.Value;
                if (string.IsNullOrEmpty(perfilIdStr)) return NotFound();

                int idPaciente = int.Parse(perfilIdStr);

                // Buscamos al paciente incluyendo su relación con la tabla USUARIOS
                var paciente = _context.Pacientes
                    .Include(p => p.Usuario)
                    .FirstOrDefault(p => p.id == idPaciente);

                if (paciente == null) return NotFound();

                // 🛠️ CÁLCULO DINÁMICO DE LA EDAD
                int edadCalculada = DateTime.Today.Year - paciente.fecha_nacimiento.Year;
                if (DateTime.Today.DayOfYear < paciente.fecha_nacimiento.DayOfYear)
                {
                    edadCalculada--; // Restamos un año si no ha cumplido años este año
                }

                var usuarioVM = new UsuarioResumenViewModel
                {
                    Id = paciente.id,
                    Nombre = paciente.nombre,
                    Email = paciente.Usuario?.email ?? string.Empty,
                    Edad = edadCalculada,
                    Sexo = paciente.genero, // Mapeado a tu columna alterada en la BD
                    Peso = paciente.peso_inicial, // Carga real de MySQL
                    Altura = paciente.altura_inicial, // Carga real de MySQL
                    HistorialMedico = paciente.historial_medico_breve // Carga real de MySQL
                };

                return View("Perfil", usuarioVM);
            }
            return Forbid();
        }

        // POST: Configuracion/AgregarRango (Solo Médicos)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "medico,Medico")]
        public IActionResult AgregarRango(RangoPulsoConfig nuevoRango)
        {
            if (ModelState.IsValid)
            {
                if (nuevoRango.Maximo == 0) nuevoRango.Maximo = 200;
                _config.AgregarRango(nuevoRango);
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Configuracion/AgregarTipoAlerta (Solo Médicos)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "medico,Medico")]
        public IActionResult AgregarTipoAlerta(TipoAlertaConfig nuevoTipo)
        {
            if (ModelState.IsValid)
            {
                _config.AgregarTipoAlerta(nuevoTipo);
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Configuracion/ActualizarPerfil (Solo Pacientes)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "paciente,Paciente")]
        public IActionResult ActualizarPerfil(UsuarioResumenViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Error en los datos ingresados, revisa el formato";
                return RedirectToAction(nameof(Index));
            }

            // 1. Buscamos al paciente de forma aislada
            var pacienteBD = _context.Pacientes.FirstOrDefault(p => p.id == model.Id);

            if (pacienteBD != null)
            {
                // 2. Modificamos directamente sus propiedades en la tabla PACIENTES (incluyendo tus campos clínicos)
                pacienteBD.nombre = model.Nombre;
                pacienteBD.genero = model.Sexo;
                pacienteBD.peso_inicial = model.Peso;
                pacienteBD.altura_inicial = model.Altura;
                pacienteBD.historial_medico_breve = model.HistorialMedico;

                // 3. Buscamos de forma aislada su cuenta de usuario vinculada
                var usuarioBD = _context.Usuarios.FirstOrDefault(u => u.id == pacienteBD.usuario_id);
                if (usuarioBD != null)
                {
                    usuarioBD.email = model.Email;
                }

                // 4. Guardamos físicamente los cambios en MySQL
                _context.SaveChanges();

                TempData["Mensaje"] = "¡Perfil, género y expediente actualizados correctamente en MySQL! ❤️✨";
            }
            else
            {
                TempData["Error"] = "No se encontró el expediente del paciente";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}