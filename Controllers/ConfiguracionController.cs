using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Text.Json;
using VitalBand.Models;
using VitalBand.Services;

namespace VitalBand.Controllers
{
    [Authorize]
    public class ConfiguracionController : Controller
    {
        private readonly IConfiguracionService _config;
        private readonly IHttpClientFactory _clientFactory;
        private const string BaseUrl = "https://localhost:7116/api/ConfiguracionApi"; // ⚠️ Verifica tu puerto local

        public ConfiguracionController(IConfiguracionService config, IHttpClientFactory clientFactory)
        {
            _config = config;
            _clientFactory = clientFactory;
        }

        // GET: Configuracion
        public async Task<IActionResult> Index()
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
                var client = _clientFactory.CreateClient();

                var response = await client.GetAsync($"{BaseUrl}/paciente/{idPaciente}");
                if (!response.IsSuccessStatusCode) return NotFound();

                // Leemos la respuesta unificada que trae el paciente y la cédula del médico
                var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>();
                var paciente = JsonSerializer.Deserialize<Paciente>(jsonDoc.GetProperty("paciente").GetRawText());
                string? cedulaActual = jsonDoc.GetProperty("cedulaActual").GetString();

                if (paciente == null) return NotFound();

                // Calculamos la edad de manera segura
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
            return RedirectToAction("Index");
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
            return RedirectToAction("Index");
        }

        // GET: Configuracion/VerificarCedula
        [HttpGet]
        [Authorize(Roles = "paciente,Paciente")]
        public async Task<IActionResult> VerificarCedula(string cedula)
        {
            if (string.IsNullOrEmpty(cedula))
            {
                return Json(new { existe = false, mensaje = "Por favor, ingresa una cédula." });
            }

            var client = _clientFactory.CreateClient();
            var response = await client.GetAsync($"{BaseUrl}/verificar-cedula?cedula={Uri.EscapeDataString(cedula)}");

            if (response.IsSuccessStatusCode)
            {
                return Json(await response.Content.ReadFromJsonAsync<object>());
            }

            return Json(new { existe = false, mensaje = "Error al conectar con el servicio de verificación. ❌" });
        }

        // POST: Configuracion/ActualizarPerfil (Solo Pacientes)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "paciente,Paciente")]
        public async Task<IActionResult> ActualizarPerfil(UsuarioResumen model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Error en los datos ingresados, revisa el formato";
                return RedirectToAction("Index");
            }

            var client = _clientFactory.CreateClient();
            var response = await client.PutAsJsonAsync($"{BaseUrl}/paciente/{model.Id}", model);

            if (response.IsSuccessStatusCode)
            {
                TempData["Mensaje"] = "¡Perfil, género y expediente actualizados correctamente a través de la API! ❤️✨";
            }
            else
            {
                TempData["Error"] = "No se pudo actualizar el expediente del paciente a través del servicio.";
            }

            return RedirectToAction("Index");
        }
    }
}