using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Roles = "Medico,medico")]
    public class DatosGeneralesController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        // Inyectamos el HttpClient factory
        public DatosGeneralesController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public async Task<IActionResult> Index(int id)
        {
            var client = _clientFactory.CreateClient();

            // 1. Solicitamos el expediente del paciente a través de la API de Configuración
            // ⚠️ Ajusta al puerto que use tu localhost
            string urlPaciente = $"https://localhost:7116/api/ConfiguracionApi/paciente/{id}";
            var responsePaciente = await client.GetAsync(urlPaciente);

            if (!responsePaciente.IsSuccessStatusCode) return NotFound();

            var paciente = await responsePaciente.Content.ReadFromJsonAsync<Paciente>();
            if (paciente == null) return NotFound();

            // Calculamos la edad de la misma forma
            int edadCalculada = DateTime.Today.Year - paciente.fecha_nacimiento.Year;
            if (DateTime.Today.DayOfYear < paciente.fecha_nacimiento.DayOfYear) edadCalculada--;

            // 2. Solicitamos la lista de alertas global para calcular cuántas pertenecen a este paciente
            string urlAlertas = "https://localhost:7116/api/AlertasApi";
            var responseAlertas = await client.GetAsync(urlAlertas);

            int conteoAlertas = 0;
            if (responseAlertas.IsSuccessStatusCode)
            {
                var alertas = await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>();
                conteoAlertas = alertas?.Count(a => a.paciente_id == paciente.id) ?? 0;
            }

            // Mantenemos tu simulador interno de datos por el momento
            var lecturas = GenerarLecturas(paciente.id);

            // Armamos el modelo original "DatosGenerales" intacto para la vista
            var modelo = new DatosGenerales
            {
                UsuarioId = paciente.id,
                Nombre = paciente.nombre,
                Edad = edadCalculada,
                Genero = paciente.genero ?? "No especificado",
                DescripcionMedica = paciente.historial_medico_breve ?? "Sin antecedentes registrados en el expediente.",
                FechaRegistro = paciente.Usuario?.fecha_registro ?? DateTime.Now,
                TotalAlertas = conteoAlertas,
                LecturasHoy = lecturas
            };

            // Conservamos exactamente los mismos ViewBags que alimentan tu gráfico Chart.js
            ViewBag.HorasJson = System.Text.Json.JsonSerializer.Serialize(lecturas.Select(l => l.Hora.ToString("HH:mm")));
            ViewBag.PulsosJson = System.Text.Json.JsonSerializer.Serialize(lecturas.Select(l => l.Pulso));

            return View(modelo);
        }

        // Método auxiliar del simulador de sensores (se queda igual)
        private List<LecturaDiaria> GenerarLecturas(int pacienteId)
        {
            var random = new Random(pacienteId);
            var ahora = DateTime.Now;
            var lecturas = new List<LecturaDiaria>();

            for (int i = 0; i < 24; i++)
            {
                int pulsoBase = random.Next(65, 85);
                lecturas.Add(new LecturaDiaria
                {
                    Hora = ahora.Date.AddHours(i),
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

            // Armamos el objeto Alerta crudo
            var nuevaAlerta = new Alerta
            {
                paciente_id = usuarioId,
                fecha_hora = DateTime.Now,
                fc_media = pulso,
                hrv_rmssd = 25.0f,
                spo2_estabilidad = 95.0f,
                latitud = 21.1219f,
                longitud = -101.6825f,
                mensaje_enviado = false
            };

            // Enviamos un POST seguro a nuestra API
            string urlApi = "https://localhost:7116/api/AlertasApi/manual";
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