using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.AspNetCore.Mvc;
using System;
using VitalBand.Models;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Linq;

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
        public async Task<IActionResult> RegistrarVectoresReposo([FromBody] TelemetriaSaludDto data)
        {
            if (data == null) return BadRequest("El vector de datos no puede estar vacío.");

            var url = _configuration["InfluxDB:Url"];
            var token = _configuration["InfluxDB:Token"];
            var org = _configuration["InfluxDB:Org"];
            var bucket = _configuration["InfluxDB:Bucket"];

            using var client = new InfluxDBClient(url, token);

            try
            {
                var point = PointData.Measurement("signos_vitales")
                    .Tag("userId", data.UserId)
                    .Field("bpm", data.Bpm)
                    .Field("rmssd", data.Rmssd)
                    .Field("spo2", data.Spo2)
                    .Timestamp(DateTime.UtcNow, WritePrecision.Ms);

                var writeApi = client.GetWriteApiAsync();
                await writeApi.WritePointAsync(point, bucket, org);

                return Ok(new { status = "Éxito", message = "Vector tridimensional guardado correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "Error", error = ex.Message });
            }
        }

        [HttpGet("hoy/{userId}")]
        public async Task<IActionResult> ObtenerRegistrosHoy(string userId, [FromQuery] string fecha = null)
        {
            if (string.IsNullOrEmpty(userId)) return BadRequest("El ID de usuario es requerido.");

            var url = _configuration["InfluxDB:Url"];
            var token = _configuration["InfluxDB:Token"];
            var org = _configuration["InfluxDB:Org"];
            var bucket = _configuration["InfluxDB:Bucket"];

            using var client = new InfluxDBClient(url, token);

            try
            {
                DateTime diaBase = DateTime.Today;
                if (!string.IsNullOrEmpty(fecha) && DateTime.TryParse(fecha, out var fechaParseada))
                {
                    diaBase = fechaParseada.Date;
                }

                DateTime inicioHoyUtc = new DateTime(diaBase.Year, diaBase.Month, diaBase.Day, 0, 0, 0, DateTimeKind.Utc);
                DateTime finHoyUtc = inicioHoyUtc.AddDays(1).AddTicks(-1);

                string startIso = inicioHoyUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
                string stopIso = finHoyUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

                string query = $@"
            from(bucket: ""{bucket}"")
              |> range(start: {startIso}, stop: {stopIso})
              |> filter(fn: (r) => r[""_measurement""] == ""signos_vitales"")
              |> filter(fn: (r) => r[""userId""] == ""{userId}"")
              |> pivot(rowKey:[""_time""], columnKey: [""_field""], valueColumn: ""_value"")";

                var queryApi = client.GetQueryApi();
                var tables = await queryApi.QueryAsync(query, org);

                var resultado = tables
                    .SelectMany(table => table.Records)
                    .Select(record => {
                        var fluxTime = record.GetTime();

                        return new
                        {
                            fecha = fluxTime != null ? fluxTime.Value.ToDateTimeUtc() : DateTime.UtcNow,
                            userId = record.GetValueByKey("userId")?.ToString(),
                            bpm = record.GetValueByKey("bpm"),
                            rmssd = record.GetValueByKey("rmssd"),
                            spo2 = record.GetValueByKey("spo2")
                        };
                    })
                    .OrderBy(r => r.fecha)
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
            var org = _configuration["InfluxDB:Org"];
            var bucket = _configuration["InfluxDB:Bucket"];

            using var client = new InfluxDBClient(url, token);

            try
            {
                DateTime inicioMesLocal = new DateTime(ano, mes, 1, 0, 0, 0);
                DateTime finMesLocal = inicioMesLocal.AddMonths(1).AddTicks(-1);

                DateTime startUtc = inicioMesLocal.AddHours(6);
                DateTime stopUtc = finMesLocal.AddHours(6);

                string startIso = startUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
                string stopIso = stopUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

                string query = $@"
            from(bucket: ""{bucket}"")
              |> range(start: {startIso}, stop: {stopIso})
              |> filter(fn: (r) => r[""_measurement""] == ""signos_vitales"")
              |> filter(fn: (r) => r[""userId""] == ""{userId}"")
              |> filter(fn: (r) => r[""_field""] == ""bpm"")
              |> yield(name: ""mean"")";

                var queryApi = client.GetQueryApi();
                var tables = await queryApi.QueryAsync(query, org);

                var registrosCrudos = tables
                    .SelectMany(table => table.Records)
                    .Select(record => {
                        var fluxTime = record.GetTime();
                        DateTime fechaLocal = fluxTime != null ? fluxTime.Value.ToDateTimeUtc().AddHours(-6) : DateTime.MinValue;

                        return new
                        {
                            DiaLocal = fechaLocal.Day,
                            Valor = record.GetValue() != null ? Convert.ToDouble(record.GetValue()) : 0
                        };
                    })
                    .Where(r => r.DiaLocal > 0)
                    .ToList();

                var resultado = registrosCrudos
                    .GroupBy(r => r.DiaLocal)
                    .Select(g => new
                    {
                        dia = g.Key,
                        bpmPromedio = (int)Math.Round(g.Average(r => r.Valor))
                    })
                    .OrderBy(r => r.dia)
                    .ToList();

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}