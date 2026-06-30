using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using VitalBand.Models; // Para que reconozca tus modelos de C#

namespace VitalBand.Controllers
{
    public class HistorialAlertasController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        // Inyectamos el cliente HTTP en lugar del DbContext
        public HistorialAlertasController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        // GET: HistorialAlertas
        // Este método carga tu vista Razor de siempre (ej. Index.cshtml)
        public async Task<IActionResult> Index()
        {
            var client = _clientFactory.CreateClient();

            // ⚠️ IMPORTANTE: Cambia el puerto (7116) por el que use tu proyecto localmente
            string urlApi = "https://localhost:7116/api/AlertasApi";

            var response = await client.GetAsync(urlApi);

            if (response.IsSuccessStatusCode)
            {
                // La API nos devuelve una lista de Alertas (con Paciente incluido)
                var listaAlertas = await response.Content.ReadFromJsonAsync<List<Alerta>>();

                // Le pasamos la lista de alertas a tu vista Razor exactamente como antes
                return View(listaAlertas);
            }

            // Si falla la API, mandamos una lista vacía para que no truene la página
            return View(new List<Alerta>());
        }
    }
}