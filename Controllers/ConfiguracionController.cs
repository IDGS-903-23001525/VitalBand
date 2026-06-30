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
                var vm = new ConfiguracionGlobal
                {
                    RangosPulso = _config.ObtenerRangosPulso() ?? new List<RangoPulso>(),
                    TiposAlerta = _config.ObtenerTiposAlerta() ?? new List<TipoAlerta>()
                };
                return View("Index", vm);
            }
            else if (User.IsInRole("Paciente") || User.IsInRole("paciente"))
            {
                var perfilIdStr = User.FindFirst("PerfilId")?.Value;
                if (string.IsNullOrEmpty(perfilIdStr)) return NotFound();

                int idPaciente = int.Parse(perfilIdStr);

                var paciente = _context.Pacientes
                    .Include(p => p.Usuario)
                    .FirstOrDefault(p => p.id == idPaciente);

                if (paciente == null) return NotFound();

                string? cedulaActual = null;
                if (paciente.medico_asignado_id.HasValue)
                {
                    cedulaActual = _context.Medicos
                        .Where(m => m.id == paciente.medico_asignado_id)
                        .Select(m => m.cedula_profesional)
                        .FirstOrDefault();
                }

                int edadCalculada = DateTime.Today.Year - paciente.fecha_nacimiento.Year;
                if (DateTime.Today.DayOfYear < paciente.fecha_nacimiento.DayOfYear) { edadCalculada--; }

                var usuarioVM = new UsuarioResumen
                {
                    Id = paciente.id,
                    Nombre = paciente.nombre,
                    Email = paciente.Usuario?.email ?? string.Empty,
                    Edad = edadCalculada,
                    Sexo = paciente.genero,
                    Peso = paciente.peso_inicial,
                    Altura = paciente.altura_inicial,
                    HistorialMedico = paciente.historial_medico_breve,
                    // Asignamos los nuevos campos
                    CedulaMedico = cedulaActual,
                    MedicoAsignadoId = paciente.medico_asignado_id
                };

                return View("Perfil", usuarioVM);
            }
            return Forbid();
        }

        // POST: Configuracion/AgregarRango (Solo Médicos)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "medico,Medico")]
        public IActionResult AgregarRango(RangoPulso nuevoRango)
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
        public IActionResult AgregarTipoAlerta(TipoAlerta nuevoTipo)
        {
            if (ModelState.IsValid)
            {
                _config.AgregarTipoAlerta(nuevoTipo);
            }
            return RedirectToAction(nameof(Index));
        }


        [HttpGet]
        [Authorize(Roles = "paciente,Paciente")]
        public IActionResult VerificarCedula(string cedula)
        {
            if (string.IsNullOrEmpty(cedula))
            {
                return Json(new { existe = false, mensaje = "Por favor, ingresa una cédula." });
            }

            var medico = _context.Medicos.FirstOrDefault(m => m.cedula_profesional == cedula.Trim());

            if (medico != null)
            {
                return Json(new { existe = true, id = medico.id, nombre = medico.nombre });
            }

            return Json(new { existe = false, mensaje = "Médico no encontrado en VitalBand ❌" });
        }


        // POST: Configuracion/ActualizarPerfil (Solo Pacientes)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "paciente,Paciente")]
        // 🛠️ CORRECCIÓN: Cambiado de UsuarioResumenViewModel a UsuarioResumen
        public IActionResult ActualizarPerfil(UsuarioResumen model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Error en los datos ingresados, revisa el formato";
                return RedirectToAction(nameof(Index));
            }

            var pacienteBD = _context.Pacientes.FirstOrDefault(p => p.id == model.Id);

            if (pacienteBD != null)
            {
                pacienteBD.nombre = model.Nombre;
                pacienteBD.genero = model.Sexo;
                pacienteBD.peso_inicial = model.Peso;
                pacienteBD.altura_inicial = model.Altura;
                pacienteBD.historial_medico_breve = model.HistorialMedico;
                pacienteBD.medico_asignado_id = model.MedicoAsignadoId;

                var usuarioBD = _context.Usuarios.FirstOrDefault(u => u.id == pacienteBD.usuario_id);
                if (usuarioBD != null)
                {
                    usuarioBD.email = model.Email;
                }

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