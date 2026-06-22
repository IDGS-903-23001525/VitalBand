using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize]
    public class ReporteController : Controller
    {
        public IActionResult Index(int año = 2026, int mes = 5, int usuarioId = 1)
        {
            // Validar que el paciente solo vea su propio reporte
            if (User.IsInRole("Paciente"))
            {
                var claimId = User.FindFirst("UsuarioId")?.Value;
                if (int.TryParse(claimId, out int pacienteId))
                {
                    if (pacienteId != usuarioId)
                        return Forbid();
                }
                else
                    return Forbid();
            }

            var usuarios = UsuariosController.ObtenerUsuarios();
            var usuario = usuarios.FirstOrDefault(u => u.Id == usuarioId) ?? usuarios.First();

            // Simular incidentes para el usuario en el mes seleccionado
            var incidentes = new List<IncidenteCritico>();
            if (usuario.TieneAlertaHoy)
            {
                incidentes.Add(new IncidenteCritico
                {
                    FechaHora = new DateTime(año, mes, DateTime.Now.Day, 14, 30, 0),
                    Descripcion = $"Frecuencia cardíaca anormal ({usuario.PulsoPromedioHoy + 15} BPM)",
                    Tipo = "high"
                });
            }

            var modelo = new ReporteSaludViewModel
            {
                NombrePaciente = usuario.Nombre,
                EdadPaciente = usuario.Edad,
                Periodo = new DateTime(año, mes, 1).ToString("MMMM yyyy"),
                Identificador = $"VB-{año}-{mes:00}-{usuarioId}",
                Incidentes = incidentes
            };

            return View("ReporteSalud", modelo);
        }
    }
}