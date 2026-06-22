using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize]
    public class HistorialAlertasController : Controller
    {
        public IActionResult Index(int usuarioId)
        {
            var alertas = new List<AlertaHistorialViewModel>
            {
                new() { Id = 101, FechaHora = new DateTime(2026, 5, 31, 14, 30, 0), Ubicacion = "Av. Reforma 123, CDMX", Respondida = true, DescripcionEvento = "Taquicardia de 120 BPM, paciente recostado." },
                new() { Id = 102, FechaHora = new DateTime(2026, 5, 25, 8, 15, 0),  Ubicacion = "Calle 5 de Mayo 45, CDMX", Respondida = false, DescripcionEvento = "Bradicardia de 45 BPM, no responde." },
                new() { Id = 103, FechaHora = new DateTime(2026, 5, 18, 22, 0, 0),  Ubicacion = "Insurgentes Sur 800, CDMX", Respondida = true, DescripcionEvento = "Arritmia leve, se estabilizó solo." }
            };
            ViewBag.UsuarioId = usuarioId;
            return View(alertas);
        }
    }
}