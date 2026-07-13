using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VitalBand.Models;
using VitalBand.Services;

namespace VitalBand.Controllers
{
    [Authorize(Roles = "medico,Medico")]
    public class AtenderAlertaController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IApiUrlProvider _apiUrlProvider;

        public AtenderAlertaController(IHttpClientFactory clientFactory, IApiUrlProvider apiUrlProvider)
        {
            _clientFactory = clientFactory;
            _apiUrlProvider = apiUrlProvider;
        }

        public async Task<IActionResult> Index(int alertaId)
        {
            var client = _clientFactory.CreateClient();
            string url = _apiUrlProvider.GetApiUrl("/api/AlertasApi");
            var response = await client.GetFromJsonAsync<List<Alerta>>(url);

            var alertaEspecifica = response?.FirstOrDefault(a => a.id == alertaId);

            if (alertaEspecifica == null) return NotFound();

            var model = new AtenderAlerta
            {
                AlertaId = alertaEspecifica.id,
                PulsoRegistrado = (int)alertaEspecifica.fc_media.GetValueOrDefault(),
                Latitud = alertaEspecifica.latitud,
                Longitud = alertaEspecifica.longitud,
                FechaHoraAlerta = alertaEspecifica.fecha_hora,
                Atendida = alertaEspecifica.mensaje_enviado == true,
                RespuestaUsuario = "Sin respuesta o activación de pánico"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarSeguimiento(AtenderAlerta model)
        {
            var client = _clientFactory.CreateClient();

            string urlApi = _apiUrlProvider.GetApiUrl($"/api/AlertasApi/atender/{model.AlertaId}");

            var response = await client.PutAsJsonAsync(urlApi, new { });

            if (response.IsSuccessStatusCode)
            {
                TempData["Mensaje"] = "¡La alerta médica ha sido atendida y registrada con éxito!";
            }
            else
            {
                TempData["Error"] = "Hubo un problema al intentar registrar la atención de la alerta.";
            }

            return RedirectToAction("Index", "Usuarios");
        }
    }
}