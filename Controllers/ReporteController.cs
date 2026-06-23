using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using VitalBand.Data;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize]
    public class ReporteController : Controller
    {
        private readonly VitalBandContext _context;

        public ReporteController(VitalBandContext context)
        {
            _context = context;
        }

        public IActionResult Index(int año = 2026, int mes = 5, int usuarioId = 1)
        {
            if (User.IsInRole("Paciente") || User.IsInRole("paciente"))
            {
                var claimId = User.FindFirst("PerfilId")?.Value;
                if (int.TryParse(claimId, out int pacienteId))
                {
                    if (pacienteId != usuarioId)
                        return Forbid();
                }
                else
                {
                    return Forbid();
                }
            }

            var pacienteBD = _context.Pacientes.FirstOrDefault(p => p.id == usuarioId);
            if (pacienteBD == null) return NotFound("No se encontró el expediente del paciente.");

            int edadCalculada = DateTime.Today.Year - pacienteBD.fecha_nacimiento.Year;
            if (DateTime.Today.DayOfYear < pacienteBD.fecha_nacimiento.DayOfYear) edadCalculada--;

            var alertasMesBD = _context.Alertas
                .Where(a => a.paciente_id == usuarioId &&
                            a.fecha_hora.HasValue &&
                            a.fecha_hora.Value.Year == año &&
                            a.fecha_hora.Value.Month == mes)
                .OrderBy(a => a.fecha_hora)
                .ToList();

            var incidentesReporte = alertasMesBD.Select(a => new IncidenteCritico
            {
                FechaHora = a.fecha_hora ?? DateTime.Now,
                Descripcion = $"Frecuencia cardíaca anómala: {a.fc_media} BPM. SpO2: {a.spo2_estabilidad}% y HRV: {a.hrv_rmssd}.",
                Tipo = a.fc_media >= 100 ? "high" : (a.fc_media <= 55 ? "low" : "irregular")
            }).ToList();

            var modelo = new ReporteSalud
            {
                NombrePaciente = pacienteBD.nombre,
                EdadPaciente = edadCalculada,
                Periodo = new DateTime(año, mes, 1).ToString("MMMM yyyy"),
                Identificador = $"VB-{año}-{mes:00}-{usuarioId}",
                Incidentes = incidentesReporte
            };

            return View("ReporteSalud", modelo);
        }
    }
}