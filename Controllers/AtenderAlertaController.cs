using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize]
    public class AtenderAlertaController : Controller
    {
        public IActionResult Index(int alertaId)
        {
            var modelo = new AtenderAlertaViewModel
            {
                AlertaId = alertaId,
                Latitud = 19.4326,      // Ejemplo: CDMX
                Longitud = -99.1332,
                PulsoRegistrado = alertaId == 101 ? 120 : (alertaId == 102 ? 45 : 88),
                DuracionSegundos = 35,
                Atendida = alertaId != 102,   // la 102 no atendida
                RespuestaUsuario = "Estaba subiendo escaleras, me mareé",
                FechaHoraAlerta = new DateTime(2026, 5, 31, 14, 30, 0)
            };
            return View(modelo);
        }
    }
}