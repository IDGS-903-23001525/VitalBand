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
    [Authorize(Roles = "medico,Medico")]
    public class UsuariosController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        public UsuariosController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public async Task<IActionResult> Index()
        {
            DateTime hoy = DateTime.Today;

            var medicoIdStr = User.FindFirst("PerfilId")?.Value;
            if (string.IsNullOrEmpty(medicoIdStr)) return Challenge();
            int idMedicoLogueado = int.Parse(medicoIdStr);

            var client = _clientFactory.CreateClient();

            // 1. SOLUCIÓN: Traemos TODOS los pacientes reales desde la API de pacientes
            string urlPacientes = "https://localhost:7116/api/ConfiguracionApi/pacientes";
            var responsePacientes = await client.GetAsync(urlPacientes);
            if (!responsePacientes.IsSuccessStatusCode) return View(new List<UsuarioResumen>());

            var todosLosPacientes = await responsePacientes.Content.ReadFromJsonAsync<List<Paciente>>() ?? new List<Paciente>();

            // 2. FILTRO POR MÉDICO: Aquí filtramos de forma segura solo los asignados a este médico
            var susPacientes = todosLosPacientes
                .Where(p => p.medico_asignado_id == idMedicoLogueado || p.medico_asignado_id == idMedicoLogueado)
                .ToList();

            // 3. Traemos las alertas de hoy para saber quién tiene criticidad
            string urlAlertas = "https://localhost:7116/api/AlertasApi";
            var responseAlertas = await client.GetAsync(urlAlertas);
            var todasLasAlertas = responseAlertas.IsSuccessStatusCode
                ? await responseAlertas.Content.ReadFromJsonAsync<List<Alerta>>() ?? new List<Alerta>()
                : new List<Alerta>();

            // 4. Armamos el resumen (ahora sí incluirá a Luis Torres con 0 alertas)
            var usuariosResumen = susPacientes.Select(p =>
            {
                var alertasHoy = todasLasAlertas
                    .Where(a => a.paciente_id == p.id && a.fecha_hora >= hoy)
                    .ToList();

                int edadCalculada = DateTime.Today.Year - p.fecha_nacimiento.Year;
                if (DateTime.Today.DayOfYear < p.fecha_nacimiento.DayOfYear) edadCalculada--;

                return new UsuarioResumen
                {
                    Id = p.id,
                    Nombre = p.nombre,
                    Email = p.Usuario?.email ?? string.Empty,
                    Edad = edadCalculada,
                    Sexo = p.genero ?? "No Especificado",
                    TieneAlertaHoy = alertasHoy.Any(),
                    PulsoPromedioHoy = alertasHoy.Any() ? (int)alertasHoy.Average(a => a.fc_media) : 70
                };
            }).ToList();

            var usuariosOrdenados = usuariosResumen.OrderByDescending(u => u.TieneAlertaHoy).ToList();
            return View(usuariosOrdenados);
        }
    }
}