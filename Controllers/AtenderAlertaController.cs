using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize(Roles = "medico,Medico")]
    public class AtenderAlertaController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        // Cambiamos el contexto de BD por el cliente HTTP factory
        public AtenderAlertaController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        // 🛠️ Cambiamos 'int id' por 'int alertaId' para atrapar la QueryString '?alertaId=2' de la imagen
        public async Task<IActionResult> Index(int alertaId)
        {
            var client = _clientFactory.CreateClient();
            string url = $"https://localhost:7116/api/AlertasApi";
            var response = await client.GetFromJsonAsync<List<Alerta>>(url);

            // 🛠️ Buscamos la alerta exacta usando la variable corregida
            var alertaEspecifica = response?.FirstOrDefault(a => a.id == alertaId);

            if (alertaEspecifica == null) return NotFound();

            var model = new AtenderAlerta
            {
                AlertaId = alertaEspecifica.id,
                PulsoRegistrado = (int)alertaEspecifica.fc_media,
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

            // Llamamos al método PUT que ya creamos en tu API para marcar la alerta como atendida
            // La ruta es: api/AlertasApi/atender/{id}
            string urlApi = $"https://localhost:7116/api/AlertasApi/atender/{model.AlertaId}";

            // Como es un método PUT sin un cuerpo complejo (la API solo necesita el ID en la URL), 
            // mandamos un contenido vacío (StringContent) o un Json vacío
            var response = await client.PutAsJsonAsync(urlApi, new { });

            if (response.IsSuccessStatusCode)
            {
                TempData["Mensaje"] = "¡La alerta médica ha sido atendida y registrada con éxito! ❤️✨";
            }
            else
            {
                TempData["Error"] = "Hubo un problema al intentar registrar la atención de la alerta.";
            }

            return RedirectToAction("Index", "Usuarios");
        }
    }
}