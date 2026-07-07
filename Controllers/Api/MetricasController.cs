using InfluxDB3.Client;
using InfluxDB3.Client.Config;
using InfluxDB3.Client.Write;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using VitalBand.Models;

namespace VitalBand.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class VitalSignController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public VitalSignController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("registrar-reposo")]
        public async Task<IActionResult> RegistrarVectoresReposo([FromBody] TelemetriaSaludDto? data)
        {
            if (data is null)
            {
                return BadRequest("El vector de datos no puede estar vacío.");
            }

            var url = _configuration["InfluxDB:Url"];
            var token = _configuration["InfluxDB:Token"];
            var database = _configuration["InfluxDB:Database"] ?? _configuration["InfluxDB:Bucket"];

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(database))
            {
                return StatusCode(500, new { status = "Error", error = "Faltan configuraciones de InfluxDB." });
            }

            try
            {
                using var client = new InfluxDBClient(new ClientConfig
                {
                    Host = url,
                    Token = token,
                    Database = database
                });

                var point = PointData.Measurement("signos_vitales")
                    .SetTag("userId", data.UserId)
                    .SetField("bpm", data.Bpm)
                    .SetField("rmssd", data.Rmssd)
                    .SetField("spo2", data.Spo2)
                    .SetTimestamp(DateTime.UtcNow);

                await client.WritePointAsync(point: point);

                return Ok(new { status = "Éxito", message = "Vector tridimensional guardado correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "Error", error = ex.Message });
            }
        }

        [HttpGet("hoy/{userId}")]
        public async Task<IActionResult> ObtenerRegistrosHoy(string userId, [FromQuery] string? fecha = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("El ID de usuario es requerido.");
            }

            var url = _configuration["InfluxDB:Url"];
            var token = _configuration["InfluxDB:Token"];
            var database = _configuration["InfluxDB:Database"] ?? _configuration["InfluxDB:Bucket"];

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(database))
            {
                return StatusCode(500, new { status = "Error", error = "Faltan configuraciones de InfluxDB." });
            }

            try
            {
                using var client = new InfluxDBClient(new ClientConfig
                {
                    Host = url,
                    Token = token,
                    Database = database
                });

                DateTime diaBase = DateTime.Today;
                if (!string.IsNullOrWhiteSpace(fecha) && DateTime.TryParse(fecha, out var fechaParseada))
                {
                    diaBase = fechaParseada.Date;
                }

                DateTime inicioHoyUtc = new DateTime(diaBase.Year, diaBase.Month, diaBase.Day, 0, 0, 0, DateTimeKind.Utc);
                DateTime finHoyUtc = inicioHoyUtc.AddDays(1);

                string query = """
                    SELECT time, "userId", bpm, rmssd, spo2
                    FROM signos_vitales
                    WHERE time >= $start AND time < $stop AND "userId" = $userId
                    ORDER BY time ASC
                    """;

                var rows = new List<object?[]>();
                await foreach (var row in client.Query(
                    query: query,
                    namedParameters: new Dictionary<string, object>
                    {
                        ["start"] = inicioHoyUtc.ToString("o"),
                        ["stop"] = finHoyUtc.ToString("o"),
                        ["userId"] = userId
                    }))
                {
                    rows.Add(row);
                }

                var resultado = rows
                    .Select(row => new
                    {
                        fecha = ToIsoMilisegundos(row[0]),
                        userId = row[1],
                        bpm = row[2],
                        rmssd = row[3],
                        spo2 = row[4]
                    })
                    .OrderBy(x => x.fecha)
                    .ToList();

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "Error", error = ex.Message });
            }
        }

        [HttpGet("mensual/{userId}/{ano}/{mes}")]
        public async Task<IActionResult> ObtenerResumenMensual(string userId, int ano, int mes)
        {
            var url = _configuration["InfluxDB:Url"];
            var token = _configuration["InfluxDB:Token"];
            var database = _configuration["InfluxDB:Database"] ?? _configuration["InfluxDB:Bucket"];

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(database))
            {
                return StatusCode(500, new { error = "Faltan configuraciones de InfluxDB." });
            }

            try
            {
                using var client = new InfluxDBClient(new ClientConfig
                {
                    Host = url,
                    Token = token,
                    Database = database
                });

                DateTime inicioMesUtc = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);
                DateTime finMesUtc = inicioMesUtc.AddMonths(1);

                string query = """
                    SELECT time, bpm
                    FROM signos_vitales
                    WHERE time >= $start AND time < $stop AND "userId" = $userId
                    ORDER BY time ASC
                    """;

                var registrosPorDia = new Dictionary<int, List<double>>();
                await foreach (var row in client.Query(
                    query: query,
                    namedParameters: new Dictionary<string, object>
                    {
                        ["start"] = inicioMesUtc.ToString("o"),
                        ["stop"] = finMesUtc.ToString("o"),
                        ["userId"] = userId
                    }))
                {
                    if (row.Length < 2 || row[0] is null || row[1] is null)
                    {
                        continue;
                    }

                    DateTime fecha = ToDateTimeUtc(row[0]);

                    if (double.TryParse(row[1]?.ToString(), CultureInfo.InvariantCulture, out var bpm))
                    {
                        int dia = fecha.Day;
                        if (!registrosPorDia.ContainsKey(dia))
                        {
                            registrosPorDia[dia] = new List<double>();
                        }

                        registrosPorDia[dia].Add(bpm);
                    }
                }

                var resultado = registrosPorDia
                    .OrderBy(x => x.Key)
                    .Select(x => new
                    {
                        dia = x.Key,
                        bpmPromedio = Math.Round(x.Value.Average(), 2)
                    })
                    .ToList();

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private static string ToIsoMilisegundos(object? valor)
        {
            var dt = ToDateTimeUtc(valor);
            return dt.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private static DateTime ToDateTimeUtc(object? valor)
        {
            DateTime dt;
            var s = valor?.ToString();

            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nanos) && s!.Length >= 18)
            {
                dt = DateTimeOffset.FromUnixTimeMilliseconds(nanos / 1_000_000).UtcDateTime;
            }
            else if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var micros) && s!.Length >= 15)
            {
                dt = DateTimeOffset.FromUnixTimeMilliseconds(micros / 1_000).UtcDateTime;
            }
            else
            {
                dt = valor switch
                {
                    DateTime d => d,
                    DateTimeOffset dto => dto.UtcDateTime,
                    _ => DateTime.Parse(s!, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
                };
            }

            if (dt.Kind == DateTimeKind.Local)
            {
                dt = dt.ToUniversalTime();
            }

            return dt;
        }
    }
}