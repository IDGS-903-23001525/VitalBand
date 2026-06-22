using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize]
    public class HistorialController : Controller
    {
        // Médico: puede ver cualquier usuario
        [Authorize(Roles = "Medico")]
        public IActionResult Index(int año = 2026, int mes = 5, int usuarioId = 1)
        {
            var usuarios = UsuariosController.ObtenerUsuarios();
            var usuario = usuarios.FirstOrDefault(u => u.Id == usuarioId);
            if (usuario == null) usuario = usuarios.First();
            var vm = GenerarHistorialViewModel(año, mes, usuario);
            ViewBag.Usuarios = usuarios;
            return View("HistorialMensual", vm);
        }

        // Paciente: solo su historial
        [Authorize(Roles = "Paciente")]
        public IActionResult MiHistorial(int año = 2026, int mes = 5)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var usuarios = UsuariosController.ObtenerUsuarios();
            var usuario = usuarios.FirstOrDefault(u => u.Email == email);
            if (usuario == null) return RedirectToAction("Login", "Account");
            var vm = GenerarHistorialViewModel(año, mes, usuario);
            return View("HistorialMensual", vm);
        }

        private HistorialMensualViewModel GenerarHistorialViewModel(int año, int mes, UsuarioResumenViewModel usuario)
        {
            var primerDia = new DateTime(año, mes, 1);
            int diasVacios = (int)primerDia.DayOfWeek == 0 ? 6 : (int)primerDia.DayOfWeek - 1;
            int totalDias = DateTime.DaysInMonth(año, mes);
            var random = new Random(usuario.Id);
            var dias = Enumerable.Range(1, totalDias).Select(d => new DiaHistorial
            {
                Numero = d,
                BpmPromedio = usuario.PulsoPromedioHoy + random.Next(-8, 9),
                TieneAlerta = (d % 10 == 0) // ejemplo: cada 10 días alerta
            }).ToList();

            return new HistorialMensualViewModel
            {
                PeriodoNombre = primerDia.ToString("MMMM yyyy"),
                Mes = mes,
                Año = año,
                DiasVaciosInicio = diasVacios,
                PromedioMensual = (int)dias.Average(d => d.BpmPromedio),
                TotalIncidentes = dias.Count(d => d.TieneAlerta),
                EstadoSalud = "Estable",
                DiasDelMes = dias,
                UsuarioId = usuario.Id,
                UsuarioNombre = usuario.Nombre
            };
        }

        [Authorize]
        public IActionResult ExportarPdf(int año = 2026, int mes = 5, int usuarioId = 1)
        {
            // Paciente solo puede exportar su propio PDF
            if (User.IsInRole("Paciente"))
            {
                var claimId = User.FindFirst("UsuarioId")?.Value;
                if (int.TryParse(claimId, out int pacienteId))
                    usuarioId = pacienteId;
                else
                    return Forbid();
            }
            return RedirectToAction("Index", "Reporte", new { año, mes, usuarioId });
        }
    }
}