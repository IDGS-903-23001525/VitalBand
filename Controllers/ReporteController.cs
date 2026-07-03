using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VitalBand.Models;
using VitalBand.Services;

namespace VitalBand.Controllers
{
    [Authorize]
    public class ReporteController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IApiUrlProvider _apiUrlProvider;

        // Reemplazamos VitalBandContext por el HttpClient factory
        public ReporteController(IHttpClientFactory clientFactory, IApiUrlProvider apiUrlProvider)
        {
            _clientFactory = clientFactory;
            _apiUrlProvider = apiUrlProvider;
        }

        public async Task<IActionResult> Index(int año = 2026, int mes = 5, int usuarioId = 1)
        {
            // Validamos la seguridad del rol exactamente igual
            if (User.IsInRole("Paciente") || User.IsInRole("paciente"))
            {
                var claimId = User.FindFirst("PerfilId")?.Value;
                if (int.TryParse(claimId, out int pacienteId))
                {
                    if (pacienteId != usuarioId)
                        return Forbid();
                }
                else
                {
                    return Forbid();
                }
            }

            var client = _clientFactory.CreateClient();

            // 1. Solicitamos el expediente del paciente a la API de Configuración
            string urlPaciente = _apiUrlProvider.GetApiUrl($"/api/ConfiguracionApi/paciente/{usuarioId}");
            var responsePaciente = await client.GetAsync(urlPaciente);

            if (!responsePaciente.IsSuccessStatusCode)
                return NotFound("No se encontró el expediente del paciente.");

            var pacienteBD = await responsePaciente.Content.ReadFromJsonAsync<Paciente>();
            if (pacienteBD == null) return NotFound("No se pudo leer la información del expediente.");

            // Calculamos la edad de forma idéntica
            int edadCalculada = DateTime.Today.Year - pacienteBD.fecha_nacimiento.Year;
            if (DateTime.Today.DayOfYear < pacienteBD.fecha_nacimiento.DayOfYear) edadCalculada--;

            // 2. Solicitamos el listado de alertas global a la API
            string urlAlertas = _apiUrlProvider.GetApiUrl("/api/AlertasApi");
            var responseAlertas = await client.GetAsync(urlAlertas);

            if (!responseAlertas.IsSuccessStatusCode)
                return NotFound("Error al conectar con el servicio de alertas.");

            var todasLasAlertas = await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>();

            // 3. Filtramos en memoria local las alertas del mes correspondientes al paciente solicitado
            var alertasMesBD = todasLasAlertas
                .Where(a => a.paciente_id == usuarioId &&
                            a.fecha_hora.HasValue &&
                            a.fecha_hora.Value.Year == año &&
                            a.fecha_hora.Value.Month == mes)
                .OrderBy(a => a.fecha_hora)
                .ToList();

            // Mapeamos a tu submodelo plano de incidentes
            var incidentesReporte = alertasMesBD.Select(a => new IncidenteCritico
            {
                FechaHora = a.fecha_hora ?? DateTime.Now,
                Descripcion = $"Frecuencia cardíaca anómala: {a.fc_media} BPM. SpO2: {a.spo2_estabilidad}% y HRV: {a.hrv_rmssd}.",
                Tipo = a.fc_media >= 100 ? "high" : (a.fc_media <= 55 ? "low" : "irregular")
            }).ToList();

            // Armamos el ViewModel final con la cultura en español para el nombre del mes
            var modelo = new ReporteSalud
            {
                NombrePaciente = pacienteBD.nombre,
                EdadPaciente = edadCalculada,
                Periodo = new DateTime(año, mes, 1).ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-MX")),
                Identificador = $"VB-{año}-{mes:00}-{usuarioId}",
                Incidentes = incidentesReporte
            };

            return View("ReporteSalud", modelo);
        }
    }
}