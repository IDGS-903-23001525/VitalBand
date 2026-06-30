using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    public class HistorialAlertasController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private const string UrlApi = "https://localhost:7116/api/AlertasApi"; // ⚠️ Verifica tu puerto local

        public HistorialAlertasController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        // GET: HistorialAlertas
        public async Task<IActionResult> Index()
        {
            var client = _clientFactory.CreateClient();
            var response = await client.GetAsync(UrlApi);

            if (response.IsSuccessStatusCode)
            {
                // 1. Recibimos la lista de alertas base desde la API
                var listaAlertas = await response.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>();

                // 2. Mapeamos cada 'Alerta' al modelo 'AlertaHistorial' que exige tu vista Razor
                var listaHistorial = listaAlertas.Select(a => new AlertaHistorial
                {
                    Id = a.id,
                    FechaHora = a.fecha_hora ?? DateTime.Now,

                    // Concatenamos latitud y longitud para armar la cadena de ubicación
                    Ubicacion = $"Lat: {a.latitud}, Lon: {a.longitud}",

                    // El campo mensaje_enviado mapea si ya fue respondida/atendida o notificada
                    Respondida = a.mensaje_enviado ?? false,
                    // Estructuramos la descripción con los datos de los sensores de la API
                    DescripcionEvento = $"Frecuencia Cardíaca: {a.fc_media} BPM | SpO2: {a.spo2_estabilidad}% | HRV: {a.hrv_rmssd} ms"
                }).ToList();

                // 3. Enviamos la lista con el tipo de dato correcto
                return View(listaHistorial);
            }

            // Si falla la comunicación, mandamos la lista vacía del tipo correcto para que no rompa la tabla
            return View(new List<AlertaHistorial>());
        }
    }
}