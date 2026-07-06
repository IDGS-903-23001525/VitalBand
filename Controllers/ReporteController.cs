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

        [HttpGet] // 1. ASEGÚRATE DE QUE TENGA EL ATRIBUTO HTTPGET
        public async Task<IActionResult> Index(int año = 0, int mes = 0, int usuarioId = 0, int idPacienteExterno = 0)
        {

            if (año == 0) año = DateTime.Today.Year;
            if (mes == 0) mes = DateTime.Today.Month;

            int idPacienteSQL = idPacienteExterno == 0 ? 1 : idPacienteExterno;

            if (User.IsInRole("Paciente") || User.IsInRole("paciente"))
            {
                var claimId = User.FindFirst("PerfilId")?.Value;
                if (int.TryParse(claimId, out int pacienteId))
                {
                    // Si es paciente, lo obligamos a ver solo SU propio ID
                    idPacienteSQL = pacienteId;
                }
                else
                {
                    return Forbid();
                }
            }
            else
            {
                // SI ES MÉDICO: Si por algún error el parámetro 'usuarioId' llegó en 0, 
                // le ponemos 1 como último recurso, pero si viene del calendario (ej. 2), se respeta el 2.
                if (idPacienteSQL == 0) idPacienteSQL = 1;
            }

            var client = _clientFactory.CreateClient();

            // 1. Solicitamos el expediente del paciente a la API de Configuración
            string urlPaciente = _apiUrlProvider.GetApiUrl($"/api/ConfiguracionApi/paciente/{idPacienteSQL}");
            var responsePaciente = await client.GetAsync(urlPaciente);

            if (!responsePaciente.IsSuccessStatusCode)
                return NotFound("No se encontró el expediente del paciente.");

            // --- REEMPLAZO DE LECTURA DE JSON (AGREGAR AQUÍ) ---
            var opcionesJson = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var jsonCompleto = await responsePaciente.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(opcionesJson);

            if (!jsonCompleto.TryGetProperty("paciente", out var pacientePropiedad))
                return NotFound("No se encontró la propiedad 'paciente' en la respuesta.");

            // Convertimos ese pedazo de JSON interno directamente en tu objeto Paciente
            var pacienteBD = System.Text.Json.JsonSerializer.Deserialize<Paciente>(pacientePropiedad.GetRawText(), opcionesJson);
            if (pacienteBD == null) return NotFound("No se pudo deserializar la información del expediente.");

            // Calculamos la edad de forma idéntica (AQUÍ YA SE CALCULA TU EDAD DINÁMICA)
            int edadCalculada = DateTime.Today.Year - pacienteBD.fecha_nacimiento.Year;
            if (DateTime.Today.Date < pacienteBD.fecha_nacimiento.AddYears(edadCalculada)) edadCalculada--;

            // 2. Solicitamos el listado de alertas global a la API
            string urlAlertas = _apiUrlProvider.GetApiUrl("/api/AlertasApi");
            var responseAlertas = await client.GetAsync(urlAlertas);

            if (!responseAlertas.IsSuccessStatusCode)
                return NotFound("Error al conectar con el servicio de alertas.");

            var todasLasAlertas = await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>();

            // 3. Filtramos en memoria local las alertas del mes correspondientes al paciente solicitado
            var alertasMesBD = todasLasAlertas
                .Where(a => a.paciente_id == idPacienteSQL &&
                            a.fecha_hora.HasValue &&
                            a.fecha_hora.Value.Year == año &&
                            a.fecha_hora.Value.Month == mes)
                .OrderBy(a => a.fecha_hora)
                .ToList();

            // Mapeamos a tu submodelo plano de incidentes
            var incidentesReporte = alertasMesBD.Select(a => new IncidenteCritico
            {
                FechaHora = a.fecha_hora ?? DateTime.Now,
                Descripcion = $"Frecuencia cardíaca anómala: {a.fc_media.GetValueOrDefault()} BPM. SpO2: {a.spo2_estabilidad.GetValueOrDefault()}% y HRV: {a.hrv_rmssd.GetValueOrDefault()}.",
                Tipo = a.fc_media.GetValueOrDefault() >= 100 ? "high" : (a.fc_media.GetValueOrDefault() <= 55 ? "low" : "irregular")
            }).ToList();

            // ─── CONSUMIR LA TELEMETRÍA MENSUAL COMPLETAMENTE DINÁMICA ───
            var datosTelemetriaMensual = new List<Dictionary<string, object>>();

            int idParaBuscar = usuarioId == 0 ? idPacienteSQL : usuarioId;

            string urlTelemetria = _apiUrlProvider.GetApiUrl($"/api/VitalSign/mensual/{idParaBuscar}/{año}/{mes}");
            var responseTelemetria = await client.GetAsync(urlTelemetria);

            if (responseTelemetria.IsSuccessStatusCode)
            {
                datosTelemetriaMensual = await responseTelemetria.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>() ?? new List<Dictionary<string, object>>();
            }

            // --- GENERACIÓN DE DATOS DINÁMICOS DE LA GRÁFICA ---
            var datosGrafica = new List<double>();
            int diasEnMes = DateTime.DaysInMonth(año, mes);

            for (int dia = 1; dia <= diasEnMes; dia++)
            {
                var lecturaDeEsteDia = datosTelemetriaMensual.FirstOrDefault(lectura =>
                {
                    var keyDia = lectura.Keys.FirstOrDefault(k => k.Equals("dia", StringComparison.OrdinalIgnoreCase));
                    if (keyDia != null && lectura[keyDia] != null)
                    {
                        return Convert.ToInt32(lectura[keyDia].ToString()) == dia;
                    }
                    return false;
                });

                if (lecturaDeEsteDia != null)
                {
                    var keyBpm = lecturaDeEsteDia.Keys.FirstOrDefault(k => k.Equals("bpmPromedio", StringComparison.OrdinalIgnoreCase));
                    if (keyBpm != null && lecturaDeEsteDia[keyBpm] != null)
                    {
                        double bpmParseado = Convert.ToDouble(lecturaDeEsteDia[keyBpm].ToString());
                        if (bpmParseado > 0)
                        {
                            datosGrafica.Add(Math.Round(bpmParseado, 1));
                            continue;
                        }
                    }
                }

                var alertasDelDia = alertasMesBD
                    .Where(a => a.fecha_hora.HasValue && a.fecha_hora.Value.Day == dia)
                    .ToList();

                if (alertasDelDia.Any())
                {
                    double promedioDia = alertasDelDia.Average(a => a.fc_media.GetValueOrDefault());
                    datosGrafica.Add(Math.Round(promedioDia, 1));
                }
                else
                {
                    datosGrafica.Add(0);
                }
            }

            // Aquí sigue tu objeto modelo original
            var modelo = new ReporteSalud
            {
                NombrePaciente = pacienteBD?.nombre,
                EdadPaciente = edadCalculada, // <--- Revisa si aquí tenías la variable 'año' por error
                Periodo = new DateTime(año, mes, 1).ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-MX")),
                Identificador = $"VB-{año}-{mes:00}-{idPacienteSQL}",
                Incidentes = incidentesReporte,
                DatosGrafica = datosGrafica
            };

            return View("ReporteSalud", modelo);
        }
    }
}