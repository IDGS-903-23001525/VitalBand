using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize(Roles = "Medico")] // Solo médico puede acceder
    public class DatosGeneralesController : Controller
    {
        public IActionResult Index(int id)
        {
            var usuarios = UsuariosController.ObtenerUsuarios();
            var usuario = usuarios.FirstOrDefault(u => u.Id == id);
            if (usuario == null) return NotFound();

            var lecturas = GenerarLecturas(usuario);

            var modelo = new DatosGeneralesViewModel
            {
                UsuarioId = usuario.Id,
                Nombre = usuario.Nombre,
                Edad = usuario.Edad,
                Genero = usuario.Sexo,
                DescripcionMedica = "Paciente con hipertensión controlada. Sin otros antecedentes.",
                FechaRegistro = new DateTime(2025, 1, 15),
                TotalAlertas = usuario.TieneAlertaHoy ? 1 : 0,
                LecturasHoy = lecturas
            };

            ViewBag.HorasJson = System.Text.Json.JsonSerializer.Serialize(lecturas.Select(l => l.Hora.ToString("HH:mm")));
            ViewBag.PulsosJson = System.Text.Json.JsonSerializer.Serialize(lecturas.Select(l => l.Pulso));

            return View(modelo);
        }

        private List<LecturaDiariaViewModel> GenerarLecturas(UsuarioResumenViewModel usuario)
        {
            var random = new Random(usuario.Id);
            var ahora = DateTime.Now;
            var lecturas = new List<LecturaDiariaViewModel>();

            for (int i = 0; i < 24; i++)
            {
                int variacion = random.Next(-8, 9);
                int pulso = usuario.PulsoPromedioHoy + variacion;
                lecturas.Add(new LecturaDiariaViewModel
                {
                    Hora = ahora.Date.AddHours(i),
                    Pulso = pulso
                });
            }
            return lecturas;
        }

        [HttpPost]
        public IActionResult ActivarAlertaManual(int usuarioId, int pulso, string motivo)
        {
            // Aquí guardarías en BD. Simulación:
            TempData["Mensaje"] = $"Alerta manual activada para usuario {usuarioId} con pulso {pulso}. Motivo: {motivo}";
            return RedirectToAction("Index", new { id = usuarioId });
        }
    }
}