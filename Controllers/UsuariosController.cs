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
    [Authorize(Roles = "medico,Medico")]
    public class UsuariosController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IApiUrlProvider _apiUrlProvider;

        public UsuariosController(IHttpClientFactory clientFactory, IApiUrlProvider apiUrlProvider)
        {
            _clientFactory = clientFactory;
            _apiUrlProvider = apiUrlProvider;
        }

        // GET: /Usuarios o /Usuarios/Index
        public async Task<IActionResult> Index()
        {
            // 1. Extraemos el PerfilId del médico (ID de la tabla MEDICOS) directamente desde los Claims
            var medicoIdStr = User.FindFirst("PerfilId")?.Value;

            if (string.IsNullOrEmpty(medicoIdStr) || !int.TryParse(medicoIdStr, out int idMedicoLogueado))
            {
                return RedirectToAction("Login", "Account");
            }

            DateTime hoy = DateTime.Today;
            var client = _clientFactory.CreateClient();

            // 2. Traemos TODOS los pacientes reales desde la API de pacientes
            var responsePacientes = await client.GetAsync(_apiUrlProvider.GetApiUrl("/api/ConfiguracionApi/pacientes"));
            if (!responsePacientes.IsSuccessStatusCode) return View(new List<UsuarioResumen>());

            var todosLosPacientes = await responsePacientes.Content.ReadFromJsonAsync<List<Paciente>>() ?? new List<Paciente>();

            // 3. FILTRO POR MÉDICO: Filtramos en memoria solo los pacientes asignados al ID de este médico
            var susPacientes = todosLosPacientes
                .Where(p => p.medico_asignado_id == idMedicoLogueado)
                .ToList();

            // 4. Traemos las alertas globales desde la API para mapear el estado de HOY
            var responseAlertas = await client.GetAsync(_apiUrlProvider.GetApiUrl("/api/AlertasApi"));
            var todasLasAlertas = responseAlertas.IsSuccessStatusCode
                ? await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>()
                : new List<Alerta>();

            // 5. Armamos el resumen en memoria pura
            var usuariosResumen = susPacientes.Select(p =>
            {
                // Filtramos las alertas pendientes de hoy
                var alertasPacienteHoy = todasLasAlertas
                    .Where(a => a.paciente_id == p.id && a.fecha_hora >= hoy && (a.mensaje_enviado == false || a.mensaje_enviado == null))
                    .OrderByDescending(a => a.fecha_hora) // 🔥 La más reciente primero
                    .ToList();

                int edadCalculada = DateTime.Today.Year - p.fecha_nacimiento.Year;
                if (DateTime.Today.DayOfYear < p.fecha_nacimiento.DayOfYear) { edadCalculada--; }

                return new UsuarioResumen
                {
                    Id = p.id,
                    Nombre = p.nombre,
                    Email = p.Usuario?.email ?? string.Empty,
                    Edad = edadCalculada,
                    Sexo = p.genero ?? "No Especificado",
                    TieneAlertaHoy = alertasPacienteHoy.Any(),
                    PulsoPromedioHoy = alertasPacienteHoy.Any() ? (int)alertasPacienteHoy.Average(a => a.fc_media) : 70,

                    // 🛠️ CORRECCIÓN CLAVE: Guardamos el ID de la alerta que realmente está fallando hoy
                    AlertaIdPendiente = alertasPacienteHoy.FirstOrDefault()?.id ?? 0
                };
            }).ToList();

            // 6. Ordenamos: Casos críticos de HOY primero en el tablero
            var usuariosOrdenados = usuariosResumen.OrderByDescending(u => u.TieneAlertaHoy).ToList();

            return View(usuariosOrdenados);
        }

        // =========================================================================
        // 🛠️ ACCIÓN UNIFICADA: ATENDER ALERTA DESDE EL TABLERO
        // =========================================================================
        // Forzamos la ruta exacta que tu vista HTML ya está buscando (/AtenderAlerta/MarcarAtendida)
        // para que no tengas que modificar tu HTML ni renombrar el controlador.
        [HttpPost]
        [Route("AtenderAlerta/MarcarAtendida")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarAtendida(int id)
        {
            var client = _clientFactory.CreateClient();

            // 1. Mandamos la petición PUT hacia el endpoint correspondiente en tu AlertasApiController
            string urlApi = _apiUrlProvider.GetApiUrl($"/api/AlertasApi/atender/{id}");
            var response = await client.PutAsync(urlApi, null); // Pasamos null porque el ID va en la URL

            if (response.IsSuccessStatusCode)
            {
                TempData["Mensaje"] = "✅ La alerta médica ha sido marcada como atendida de forma correcta.";
            }
            else
            {
                TempData["Error"] = "No se pudo actualizar el estado de la alerta en el servicio de la API. ❌";
            }

            // 2. Redirige de vuelta a la pantalla principal del médico para refrescar el tablero
            return RedirectToAction(nameof(Index));
        }
    }
}