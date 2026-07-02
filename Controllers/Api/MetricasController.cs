using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.AspNetCore.Mvc;
using System;
using VitalBand.Models;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

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

            // 1. Extraer configuraciones de appsettings.json
            var url = _configuration["InfluxDB:Url"];
            var token = _configuration["InfluxDB:Token"];
            var org = _configuration["InfluxDB:Org"];
            var bucket = _configuration["InfluxDB:Bucket"];

            // 2. Inicializar cliente oficial de InfluxDB
            using var client = new InfluxDBClient(url, token);

            try
            {
                var point = PointData.Measurement("signos_vitales")
                    .Tag("device_id", data.DeviceId)
                    .Field("bpm", data.Bpm)
                    .Field("rmssd", data.Rmssd)
                    .Field("spo2", data.Spo2)
                    .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

                // 3. OJO: SIN el 'using' aquí. Solo obtenemos la API de escritura
                var writeApi = client.GetWriteApiAsync();

                // 4. Guardar de forma asíncrona en el bucket
                await writeApi.WritePointAsync(point, bucket, org);

                return Ok(new { status = "Éxito", message = "Vector tridimensional guardado correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "Error", error = ex.Message });
            }
        }
    }
}