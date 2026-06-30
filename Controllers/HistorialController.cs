using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize]
    public class HistorialController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        // Reemplazamos VitalBandContext por IHttpClientFactory
        public HistorialController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [Authorize(Roles = "Medico,medico")]
        public async Task<IActionResult> Index(int año = 2026, int mes = 5, int usuarioId = 1)
        {
            var medicoIdStr = User.FindFirst("PerfilId")?.Value;
            if (string.IsNullOrEmpty(medicoIdStr)) return Challenge();
            int idMedicoLogueado = int.Parse(medicoIdStr);

            var client = _clientFactory.CreateClient();

            // 1. Pedimos los pacientes reales del sistema
            string urlPacientes = "https://localhost:7116/api/ConfiguracionApi/pacientes";
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
            string urlAlertas = "https://localhost:7116/api/AlertasApi";
            var responseAlertas = await client.GetAsync(urlAlertas);
            var todasLasAlertas = responseAlertas.IsSuccessStatusCode
                ? await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>()
                : new List<Alerta>();

            var vm = GenerarHistorialModel(año, mes, pacienteSeleccionado, todasLasAlertas);

            // El buscador del médico ahora se llena ÚNICAMENTE con sus propios pacientes asignados
            ViewBag.Usuarios = susPacientes.Select(p => new UsuarioResumen { Id = p.id, Nombre = p.nombre }).ToList();

            return View("HistorialMensual", vm);
        }

        [Authorize(Roles = "Paciente,paciente")]
        public async Task<IActionResult> MiHistorial(int año = 2026, int mes = 5)
        {
            var perfilIdStr = User.FindFirst("PerfilId")?.Value;
            if (string.IsNullOrEmpty(perfilIdStr)) return RedirectToAction("Login", "Account");

            int idPaciente = int.Parse(perfilIdStr);

            var client = _clientFactory.CreateClient();

            // 1. Obtenemos el perfil físico del paciente por medio de la API de Configuración
            string urlPaciente = $"https://localhost:7116/api/ConfiguracionApi/paciente/{idPaciente}";
            var responsePaciente = await client.GetAsync(urlPaciente);

            if (!responsePaciente.IsSuccessStatusCode) return NotFound("No se encontró el perfil del paciente.");
            var paciente = await responsePaciente.Content.ReadFromJsonAsync<Paciente>();
            if (paciente == null) return NotFound();

            // 2. Obtenemos las alertas de la API
            string urlAlertas = "https://localhost:7116/api/AlertasApi";
            var responseAlertas = await client.GetAsync(urlAlertas);
            var todasLasAlertas = await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>();

            // 3. Renderizamos el modelo del calendario
            var vm = GenerarHistorialModel(año, mes, paciente, todasLasAlertas);
            return View("HistorialMensual", vm);
        }

        // Modificamos el método privado para que reciba la lista de alertas inyectada desde la API
        private HistorialMensual GenerarHistorialModel(int año, int mes, Paciente paciente, List<Alerta> alertasGlobales)
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

            for (int diaCorriente = 1; diaCorriente <= totalDias; diaCorriente++)
            {
                var alertasDeEsteDia = alertasDelMes
                    .Where(a => a.fecha_hora.Value.Day == diaCorriente)
                    .ToList();

                bool tieneAlertaEseDia = alertasDeEsteDia.Any();

                int promedioPulso = tieneAlertaEseDia
                    ? (int)alertasDeEsteDia.Average(a => a.fc_media)
                    : pulsoBaseEstable;

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

            return new HistorialMensual
            {
                PeriodoNombre = primerDia.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-MX")),
                Mes = mes,
                Año = año,
                DiasVaciosInicio = diasVacios,
                PromedioMensual = dias.Any() ? (int)dias.Average(d => d.BpmPromedio) : pulsoBaseEstable,
                TotalIncidentes = alertasDelMes.Count,
                EstadoSalud = alertasDelMes.Count > 5 ? "Riesgo Moderado" : "Estable",
                DiasDelMes = dias,
                UsuarioId = paciente.id,
                UsuarioNombre = paciente.nombre
            };
        }

        [Authorize]
        public IActionResult ExportarPdf(int año = 2026, int mes = 5, int usuarioId = 1)
        {
            if (User.IsInRole("Paciente") || User.IsInRole("paciente"))
            {
                var claimId = User.FindFirst("PerfilId")?.Value;
                if (int.TryParse(claimId, out int pacienteId))
                    usuarioId = pacienteId;
                else
                    return Forbid();
            }
            return RedirectToAction("Index", "Reporte", new { año, mes, usuarioId });
        }
    }
}