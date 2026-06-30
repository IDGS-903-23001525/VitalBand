using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using VitalBand.Models;
using VitalBand.Services;

namespace VitalBand.Controllers
{
    [Authorize]
    public class ConfiguracionController : Controller
    {
        private readonly IConfiguracionService _config;
        private readonly IHttpClientFactory _clientFactory;

        // Reemplazamos VitalBandContext por el creador de clientes HTTP
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
                // La lógica del médico se queda exactamente igual usando su servicio local
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

                // 1. Preparamos el cliente HTTP para consultar la API
                var client = _clientFactory.CreateClient();

                // ⚠️ Ajusta al puerto que use tu localhost
                string urlApi = $"https://localhost:7116/api/ConfiguracionApi/paciente/{idPaciente}";
                var response = await client.GetAsync(urlApi);

                if (!response.IsSuccessStatusCode) return NotFound();

                // 2. La API nos devuelve el objeto Paciente (con su relación Usuario gracias a EF de la API)
                var paciente = await response.Content.ReadFromJsonAsync<Paciente>();
                if (paciente == null) return NotFound();

                // Calculamos la edad exactamente igual
                int edadCalculada = DateTime.Today.Year - paciente.fecha_nacimiento.Year;
                if (DateTime.Today.DayOfYear < paciente.fecha_nacimiento.DayOfYear)
                {
                    edadCalculada--;
                }

                // Armamos el ViewModel "UsuarioResumen" que tu vista "Perfil.cshtml" espera recibir
                var usuarioVM = new UsuarioResumen
                {
                    Id = paciente.id,
                    Nombre = paciente.nombre,
                    Email = paciente.Usuario?.email ?? string.Empty,
                    Edad = edadCalculada,
                    Sexo = paciente.genero,
                    Peso = paciente.peso_inicial,
                    Altura = paciente.altura_inicial,
                    HistorialMedico = paciente.historial_medico_breve
                };

                return View("Perfil", usuarioVM);
            }
            return Forbid();
        }

        // POST: Configuracion/AgregarRango (Solo Médicos - Se queda igual)
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

        // POST: Configuracion/AgregarTipoAlerta (Solo Médicos - Se queda igual)
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

        // POST: Configuracion/ActualizarPerfil (Solo Pacientes)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "paciente,Paciente")]
        public async Task<IActionResult> ActualizarPerfil(UsuarioResumen model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Error en los datos ingresados, revisa el formato";
                return RedirectToAction(nameof(Index));
            }

            var client = _clientFactory.CreateClient();

            // ⚠️ Ajusta al puerto que use tu localhost
            string urlApi = $"https://localhost:7116/api/ConfiguracionApi/paciente/{model.Id}";

            // Enviamos el modelo "UsuarioResumen" modificado en la vista a través de un PUT
            var response = await client.PutAsJsonAsync(urlApi, model);

            if (response.IsSuccessStatusCode)
            {
                TempData["Mensaje"] = "¡Perfil, género y expediente actualizados correctamente a través de la API! ❤️✨";
            }
            else
            {
                TempData["Error"] = "No se pudo actualizar el expediente del paciente a través del servicio.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}