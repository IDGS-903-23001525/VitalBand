using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using VitalBand.Data;
using VitalBand.DTOs;
using VitalBand.Models;

namespace VitalBand.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class AlertasApiController : ControllerBase
    {
        private readonly VitalBandContext _context;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;

        public AlertasApiController(VitalBandContext context, IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetAlertas()
        {
            var alerts = await _context.Alertas
                .Include(alert => alert.Paciente)
                .OrderByDescending(alert => alert.id)
                .ToListAsync();

            return Ok(alerts);
        }

        [HttpPut("atender/{id}")]
        public async Task<IActionResult> AttendAlert(int id, [FromQuery] bool sendWebSocket = true)
        {
            var alert = await _context.Alertas.FindAsync(id);
            if (alert == null)
            {
                return NotFound(new { message = "La alerta no existe" });
            }

            alert.atendida = true;
            await _context.SaveChangesAsync();

            if (sendWebSocket)
            {
                await Middleware.WebSocketConnectionManager.SendMessageAsync(alert.paciente_id, "DISMISS");
            }

            return Ok(new { message = "Alert processed successfully.", alertId = id });
        }

        [HttpPost("manual")]
        public async Task<IActionResult> RegisterManualAlert([FromBody] ManualAlertRequest request, [FromQuery] bool sendWebSocket = true)
        {
            if (request == null || request.PatientId <= 0)
            {
                return BadRequest(new { message = "Se requiere un paciente valido." });
            }

            var alert = new Alerta
            {
                paciente_id = request.PatientId,
                fecha_hora = DateTime.UtcNow,
                fc_media = request.FcMedia,
                hrv_rmssd = request.HrvRmssd,
                spo2_estabilidad = request.Spo2Estabilidad,
                latitud = request.Latitud,
                longitud = request.Longitud,
                mensaje_enviado = false,
                atendida = false
            };

            _context.Alertas.Add(alert);
            await _context.SaveChangesAsync();

            if (sendWebSocket)
            {
                await Middleware.WebSocketConnectionManager.SendMessageAsync(alert.paciente_id, $"ALERT:{alert.id}");
            }

            var alertId = alert.id;
            var alertLat = request.Latitud;
            var alertLon = request.Longitud;
            var alertPacienteId = request.PatientId;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(30000);

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<VitalBandContext>();
                    var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

                    var alertFresh = await db.Alertas.FindAsync(alertId);
                    if (alertFresh == null || alertFresh.atendida == true) return;

                    string locationName = "Ubicación desconocida";
                    string locationAddress = "";

                    if (alertLat.HasValue && alertLon.HasValue)
                    {
                        try
                        {
                            var geoClient = httpFactory.CreateClient();
                            geoClient.DefaultRequestHeaders.Add("User-Agent", "VitalBand");
                            var geoResponse = await geoClient.GetAsync(
                                $"https://nominatim.openstreetmap.org/reverse?format=json&lat={alertLat}&lon={alertLon}");

                            if (geoResponse.IsSuccessStatusCode)
                            {
                                var geoJson = await geoResponse.Content.ReadFromJsonAsync<JsonElement>();

                                if (geoJson.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                                {
                                    var nameVal = nameProp.GetString();
                                    if (!string.IsNullOrEmpty(nameVal)) locationName = nameVal;
                                }

                                if (geoJson.TryGetProperty("display_name", out var displayProp) && displayProp.ValueKind == JsonValueKind.String)
                                {
                                    var displayVal = displayProp.GetString();
                                    if (!string.IsNullOrEmpty(displayVal)) locationAddress = displayVal;
                                }
                            }
                        }
                        catch { }
                    }

                    var contact = await db.ContactosEmergencia
                        .Where(c => c.paciente_id == alertPacienteId && c.prioridad == 1)
                        .FirstOrDefaultAsync();
                    if (contact == null) return;

                    var patient = await db.Pacientes.FindAsync(alertPacienteId);
                    string patientName = patient?.nombre ?? "Paciente";

                    var http = httpFactory.CreateClient();
                    var random = new Random();

                    var locationBody = new
                    {
                        number = contact.telefono,
                        name = locationName,
                        address = locationAddress,
                        latitude = alertLat,
                        longitude = alertLon,
                        delay = random.Next(1000, 3000)
                    };

                    var locResp = await http.PostAsJsonAsync(
                        "https://api-wa.vitalband.melchor-ruiz.dev/message/sendLocation/vitalband",
                        locationBody);
                    if (!locResp.IsSuccessStatusCode) return;

                    string texto = $"⚠️ ALERTA MÉDICA - {patientName} se encuentra en una situación de emergencia. Su ubicación actual es: {locationName}, {locationAddress}. Por favor, verifica su estado lo antes posible.";

                    var textBody = new
                    {
                        number = contact.telefono,
                        delay = random.Next(1000, 3000),
                        text = texto
                    };

                    var textResp = await http.PostAsJsonAsync(
                        "https://api-wa.vitalband.melchor-ruiz.dev/message/sendText/vitalband",
                        textBody);

                    if (textResp.IsSuccessStatusCode)
                    {
                        alertFresh.mensaje_enviado = true;
                        await db.SaveChangesAsync();
                    }
                }
                catch { }
            });

            return Ok(new { message = "Alerta Manual registrada con éxito", alertId = alert.id });
        }
    }
}