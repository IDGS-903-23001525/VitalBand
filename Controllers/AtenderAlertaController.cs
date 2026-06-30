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

        public async Task<IActionResult> Index(int alertaId)
        {
            var client = _clientFactory.CreateClient();

            // ⚠️ Recuerda verificar el puerto exacto de tu localhost local
            // Consumimos todas las alertas desde la API (puedes crear un endpoint específico api/AlertasApi/{id} si prefieres, 
            // pero para no mover tu API actual, buscaremos la alerta de la lista completa)
            string urlApi = "https://localhost:7116/api/AlertasApi";
            var response = await client.GetAsync(urlApi);

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "No se pudo conectar con el servicio de alertas.";
                return RedirectToAction("Index", "Usuarios");
            }

            var alertas = await response.Content.ReadFromJsonAsync<System.Collections.Generic.List<Alerta>>();
            var alertaBD = alertas?.FirstOrDefault(a => a.id == alertaId);

            if (alertaBD == null)
            {
                TempData["Error"] = "No se encontró el registro de la alerta biométrica.";
                return RedirectToAction("Index", "Usuarios");
            }

            // Armamos el modelo temporal exactamente con los mismos campos
            var modelo = new AtenderAlerta
            {
                AlertaId = alertaBD.id,
                Latitud = alertaBD.latitud,
                Longitud = alertaBD.longitud,
                PulsoRegistrado = (int)alertaBD.fc_media,
                DuracionSegundos = 45,
                Atendida = alertaBD.mensaje_enviado,
                RespuestaUsuario = "Alerta crítica disparada por el dispositivo móvil.",
                FechaHoraAlerta = alertaBD.fecha_hora
            };

            return View(modelo);
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