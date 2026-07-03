using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using VitalBand.Models;
using VitalBand.Services;

namespace VitalBand.Controllers
{
    [Authorize(Roles = "Medico,medico")]
    public class DatosGeneralesController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IApiUrlProvider _apiUrlProvider;

        public DatosGeneralesController(IHttpClientFactory clientFactory, IApiUrlProvider apiUrlProvider)
        {
            _clientFactory = clientFactory;
            _apiUrlProvider = apiUrlProvider;
        }

        public async Task<IActionResult> Index(int id, string fecha)
        {
            var client = _clientFactory.CreateClient();

            // 1. Solicitamos el expediente a la API de Configuración
            string urlPaciente = _apiUrlProvider.GetApiUrl($"/api/ConfiguracionApi/paciente/{id}");
            var responsePaciente = await client.GetAsync(urlPaciente);

            if (!responsePaciente.IsSuccessStatusCode) return NotFound();

            // 🛠️ CORRECCIÓN: Desarmamos el JSON compuesto que devuelve la API (trae paciente y cedulaActual)
            var jsonDoc = await responsePaciente.Content.ReadFromJsonAsync<JsonElement>();
            var paciente = JsonSerializer.Deserialize<Paciente>(jsonDoc.GetProperty("paciente").GetRawText());

            if (paciente == null) return NotFound();

            // Calculamos la edad de la misma forma
            int edadCalculada = DateTime.Today.Year - paciente.fecha_nacimiento.Year;
            if (DateTime.Today.DayOfYear < paciente.fecha_nacimiento.DayOfYear) edadCalculada--;

            // 2. Solicitamos la lista de alertas global para el conteo
            var responseAlertas = await client.GetAsync(_apiUrlProvider.GetApiUrl("/api/AlertasApi"));

            int conteoAlertas = 0;
            if (responseAlertas.IsSuccessStatusCode)
            {
                var alertas = await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>();
                conteoAlertas = alertas?.Count(a => a.paciente_id == paciente.id) ?? 0;
            }

            // Fecha seleccionada (por query string) — por defecto hoy
            DateTime fechaSeleccionada;
            if (!string.IsNullOrEmpty(fecha) && DateTime.TryParseExact(fecha, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var fechaParsed))
            {
                fechaSeleccionada = fechaParsed;
            }
            else
            {
                fechaSeleccionada = DateTime.Today;
            }

            // Simulador interno de datos para la fecha solicitada
            var lecturas = GenerarLecturas(paciente.id, fechaSeleccionada);

            // Armamos el modelo original "DatosGenerales" intacto para la vista
            var modelo = new DatosGenerales
            {
                UsuarioId = paciente.id, // Mantiene el ID del Paciente que la vista necesita
                Nombre = paciente.nombre,
                Edad = edadCalculada,
                Genero = paciente.genero ?? "No especificado",
                DescripcionMedica = paciente.historial_medico_breve ?? "Sin antecedentes registrados en el expediente.",
                FechaRegistro = paciente.Usuario?.fecha_registro ?? DateTime.Now,
                TotalAlertas = conteoAlertas,
                LecturasHoy = lecturas
            };

            // ViewBags para Chart.js
            ViewBag.HorasJson = JsonSerializer.Serialize(lecturas.Select(l => l.Hora.ToString("HH:mm")));
            ViewBag.FechaSeleccionada = fechaSeleccionada.ToString("yyyy-MM-dd");
            ViewBag.PulsosJson = JsonSerializer.Serialize(lecturas.Select(l => l.Pulso));

            return View(modelo);
        }

        // Método auxiliar del simulador de sensores
        private List<LecturaDiaria> GenerarLecturas(int pacienteId, DateTime fecha)
        {
            var random = new Random(pacienteId);
            // Usamos la fecha proporcionada para generar horas del día
            var ahora = fecha.Date;
            var lecturas = new List<LecturaDiaria>();

            for (int i = 0; i < 24; i++)
            {
                int pulsoBase = random.Next(65, 85);
                lecturas.Add(new LecturaDiaria
                {
                    Hora = ahora.AddHours(i),
                    Pulso = pulsoBase
                });
            }
            return lecturas;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivarAlertaManual(int usuarioId, int pulso, string motivo)
        {
            var client = _clientFactory.CreateClient();

            // Armamos el objeto Alerta crudo con float explícitos para tu API
            var nuevaAlerta = new Alerta
            {
                paciente_id = usuarioId, // Aquí ya recibe el id de Paciente gracias al ajuste en el HTML
                fecha_hora = DateTime.Now,
                fc_media = pulso,
                hrv_rmssd = 25.0f,
                spo2_estabilidad = 95.0f,
                latitud = 21.1219f,
                longitud = -101.6825f,
                mensaje_enviado = false
            };

            // Enviamos el POST seguro a nuestra API
            string urlApi = _apiUrlProvider.GetApiUrl("/api/AlertasApi/manual");
            var response = await client.PostAsJsonAsync(urlApi, nuevaAlerta);

            if (response.IsSuccessStatusCode)
            {
                TempData["Mensaje"] = $"⚠️ ¡Alerta médica manual guardada en MySQL mediante la API! Motivo: {motivo}";
            }
            else
            {
                TempData["Error"] = "No se pudo registrar la alerta manual en el sistema.";
            }

            return RedirectToAction("Index", new { id = usuarioId });
        }
    }
}