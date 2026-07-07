using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    public class HistorialController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IApiUrlProvider _apiUrlProvider;

        // Reemplazamos VitalBandContext por IHttpClientFactory
        public HistorialController(IHttpClientFactory clientFactory, IApiUrlProvider apiUrlProvider)
        {
            _clientFactory = clientFactory;
            _apiUrlProvider = apiUrlProvider;
        }

        [Authorize(Roles = "Medico,medico")]
        public async Task<IActionResult> Index(int año = 0, int mes = 0, int usuarioId = 1)
        {
            if (año == 0) año = DateTime.Today.Year;
            if (mes == 0) mes = DateTime.Today.Month;
            var medicoIdStr = User.FindFirst("PerfilId")?.Value;
            if (string.IsNullOrEmpty(medicoIdStr)) return Challenge();
            int idMedicoLogueado = int.Parse(medicoIdStr);

            var client = _clientFactory.CreateClient();

            // 1. Pedimos los pacientes reales del sistema
            string urlPacientes = _apiUrlProvider.GetApiUrl("/api/ConfiguracionApi/pacientes");
            var responsePacientes = await client.GetAsync(urlPacientes);
            if (!responsePacientes.IsSuccessStatusCode) return NotFound("Error en el servicio de pacientes.");
            var todosLosPacientes = await responsePacientes.Content.ReadFromJsonAsync<List<Paciente>>() ?? new List<Paciente>();

            // 2. FILTRO PRIVADO: El médico SOLO puede ver en su lista a SUS pacientes asignados
            var susPacientes = todosLosPacientes
                .Where(p => p.medico_asignado_id == idMedicoLogueado)
                .ToList();

            var pacienteSeleccionado = susPacientes.FirstOrDefault(p => p.id == usuarioId);
            if (pacienteSeleccionado == null && susPacientes.Any())
                pacienteSeleccionado = susPacientes.First();

            if (pacienteSeleccionado == null) return NotFound("No tiene pacientes asignados bajo su perfil.");

            // 3. Traemos las alertas para armar los días del calendario
            string urlAlertas = _apiUrlProvider.GetApiUrl("/api/AlertasApi");
            var responseAlertas = await client.GetAsync(urlAlertas);
            var todasLasAlertas = responseAlertas.IsSuccessStatusCode
                ? await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>()
                : new List<Alerta>();

            var vm = await GenerarHistorialModelAsync(año, mes, pacienteSeleccionado, todasLasAlertas);

            // El buscador del médico ahora se llena ÚNICAMENTE con sus propios pacientes asignados
            ViewBag.Usuarios = susPacientes.Select(p => new UsuarioResumen { Id = p.id, Nombre = p.nombre }).ToList();

            return View("HistorialMensual", vm);
        }

        [Authorize(Roles = "Paciente,paciente")]
        public async Task<IActionResult> MiHistorial(int año = 0, int mes = 0, int usuarioId = 1)
        {
            if (año == 0) año = DateTime.Today.Year;
            if (mes == 0) mes = DateTime.Today.Month;
            var perfilIdStr = User.FindFirst("PerfilId")?.Value;
            if (string.IsNullOrEmpty(perfilIdStr)) return RedirectToAction("Login", "Account");

            int idPaciente = int.Parse(perfilIdStr);
            var usuarioIdStr = User.FindFirst("UsuarioBaseId")?.Value;
            int idUsuarioReal = 0;
            if (!string.IsNullOrEmpty(usuarioIdStr))
            {
                idUsuarioReal = int.Parse(usuarioIdStr);
            }

            var client = _clientFactory.CreateClient();

            // 1. Obtenemos el perfil físico del paciente por medio de la API de Configuración
            string urlPaciente = _apiUrlProvider.GetApiUrl($"/api/ConfiguracionApi/paciente/{idPaciente}");
            var responsePaciente = await client.GetAsync(urlPaciente);

            if (!responsePaciente.IsSuccessStatusCode) return NotFound("No se encontró el perfil del paciente.");
            var paciente = await responsePaciente.Content.ReadFromJsonAsync<Paciente>();
            if (paciente == null) return NotFound();

            if (idUsuarioReal > 0)
            {
                paciente.usuario_id = idUsuarioReal;
            }

            // 2. Obtenemos las alertas de la API
            string urlAlertas = _apiUrlProvider.GetApiUrl("/api/AlertasApi");
            var responseAlertas = await client.GetAsync(urlAlertas);
            var todasLasAlertas = await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>();

            // 3. Renderizamos el modelo del calendario
            var vm = await GenerarHistorialModelAsync(año, mes, paciente, todasLasAlertas);
            return View("HistorialMensual", vm);
        }

        // Modificamos el método privado para que reciba la lista de alertas inyectada desde la API
        private async Task<HistorialMensual> GenerarHistorialModelAsync(int año, int mes, Paciente paciente, List<Alerta> alertasGlobales)
        {
            var primerDia = new DateTime(año, mes, 1);
            int diasVacios = (int)primerDia.DayOfWeek == 0 ? 6 : (int)primerDia.DayOfWeek - 1;
            int totalDias = DateTime.DaysInMonth(año, mes);

            // Filtramos en memoria local la lista que vino de la API usando los parámetros de año, mes y paciente
            var alertasDelMes = alertasGlobales
                .Where(a => a.paciente_id == paciente.id &&
                            a.fecha_hora.HasValue &&
                            a.fecha_hora.Value.Year == año &&
                            a.fecha_hora.Value.Month == mes)
                .ToList();

            var dias = new List<DiaHistorial>();
            int pulsoBaseEstable = 72;

            // ─── AÑADIR JUSTO DESPUÉS DE pulsoBaseEstable = 72; ───
            var client = _clientFactory.CreateClient();
            var datosTelemetriaMensual = new List<Dictionary<string, object>>();

            if (paciente.usuario_id > 0)
            {
                string urlTelemetria = _apiUrlProvider.GetApiUrl($"/api/VitalSign/mensual/{paciente.usuario_id}/{año}/{mes}");
                var responseTelemetria = await client.GetAsync(urlTelemetria);
                if (responseTelemetria.IsSuccessStatusCode)
                {
                    datosTelemetriaMensual = await responseTelemetria.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>() ?? new List<Dictionary<string, object>>();
                }
            }

            for (int diaCorriente = 1; diaCorriente <= totalDias; diaCorriente++)
            {
                var alertasDeEsteDia = alertasDelMes
                    .Where(a => a.fecha_hora.Value.Day == diaCorriente)
                    .ToList();

                bool tieneAlertaEseDia = alertasDeEsteDia.Any();

                // ─── REEMPLAZAR ASIGNACIÓN DE promedioPulso POR ESTO ───
                int promedioPulso = pulsoBaseEstable;

                // Buscamos si en la lista de diccionarios existe el día corriente
                var lecturaDeEsteDia = datosTelemetriaMensual.FirstOrDefault(lectura =>
                    lectura.TryGetValue("dia", out var d) && d != null && Convert.ToInt32(d.ToString(), CultureInfo.InvariantCulture) == diaCorriente);

                if (lecturaDeEsteDia != null && lecturaDeEsteDia.TryGetValue("bpmPromedio", out var bpmVal) && bpmVal != null)
                {
                    double bpmParseado = Convert.ToDouble(bpmVal.ToString(), CultureInfo.InvariantCulture);
                    if (bpmParseado > 0)
                    {
                        promedioPulso = (int)Math.Round(bpmParseado);
                    }
                }
                else if (tieneAlertaEseDia)
                {
                    promedioPulso = (int)alertasDeEsteDia.Average(a => a.fc_media.GetValueOrDefault());
                }

                string mensaje = tieneAlertaEseDia
                    ? $"⚠️ {alertasDeEsteDia.Count} Alerta(s)"
                    : string.Empty;

                dias.Add(new DiaHistorial
                {
                    Numero = diaCorriente,
                    BpmPromedio = promedioPulso,
                    TieneAlerta = tieneAlertaEseDia,
                    MensajeAlerta = mensaje
                });
            }

            ViewBag.FechaRegistro = paciente.Usuario?.fecha_registro ?? DateTime.Today.AddDays(-7);

            return new HistorialMensual
            {
                PeriodoNombre = primerDia.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-MX")),
                Mes = mes,
                Año = año,
                DiasVaciosInicio = diasVacios,
                PromedioMensual = datosTelemetriaMensual.Any(t => t.TryGetValue("bpmPromedio", out var b) && b != null && Convert.ToDouble(b.ToString(), CultureInfo.InvariantCulture) > 0)
                    ? (int)Math.Round(datosTelemetriaMensual
                        .Where(t => t.TryGetValue("bpmPromedio", out var b) && b != null && Convert.ToDouble(b.ToString(), CultureInfo.InvariantCulture) > 0)
                        .Average(t => Convert.ToDouble(t["bpmPromedio"].ToString(), CultureInfo.InvariantCulture)))
                    : pulsoBaseEstable,
                TotalIncidentes = alertasDelMes.Count,
                EstadoSalud = alertasDelMes.Count > 5 ? "Riesgo Moderado" : "Estable",
                DiasDelMes = dias,
                UsuarioId = paciente.id,
                UsuarioNombre = paciente.nombre
            };
        }

        [Authorize]
        public async Task<IActionResult> ExportarPdf(int año = 0, int mes = 0, int usuarioId = 0)
        {
            // 1. Validaciones por defecto para fechas si llegan en 0 (usando el año actual 2026)
            if (año == 0) año = DateTime.Today.Year;
            if (mes == 0) mes = DateTime.Today.Month;

            int idUsuarioReal = usuarioId;

            // 2. Si el que está logueado es Paciente, asegura su propio ID (Obteniéndolo de su Claim)
            if (User.IsInRole("Paciente") || User.IsInRole("paciente"))
            {
                var claimId = User.FindFirst("PerfilId")?.Value;
                if (int.TryParse(claimId, out int pacienteId))
                {
                    usuarioId = pacienteId; // Asignamos el ID del paciente logueado
                }
            }

            // 3. CONSULTA A LA API CENTRALIZADA (Para Médico y Paciente)
            if (usuarioId > 0)
            {
                using (var client = new HttpClient())
                {
                    string urlApiUsuario = _apiUrlProvider.GetApiUrl($"/api/UsuariosApi/ObtenerUsuarioIdPorPaciente/{usuarioId}");
                    var response = await client.GetAsync(urlApiUsuario);

                    if (response.IsSuccessStatusCode)
                    {
                        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
                        if (resultado != null && resultado.ContainsKey("usuarioIdReal"))
                        {
                            idUsuarioReal = resultado["usuarioIdReal"];
                        }
                    }
                }
            }

            // Modifica el nombre del parámetro para que coincida con la firma del ReporteController
            return RedirectToAction("Index", "Reporte", new { año = año, mes = mes, usuarioId = idUsuarioReal, idPacienteExterno = usuarioId });
        }
    }
}