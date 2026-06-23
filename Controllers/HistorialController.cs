using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using VitalBand.Data;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize]
    public class HistorialController : Controller
    {
        private readonly VitalBandContext _context;

        public HistorialController(VitalBandContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Medico,medico")]
        public IActionResult Index(int año = 2026, int mes = 5, int usuarioId = 1)
        {
            var pacientesBD = _context.Pacientes.ToList();

            var pacienteSeleccionado = pacientesBD.FirstOrDefault(p => p.id == usuarioId);
            if (pacienteSeleccionado == null && pacientesBD.Any())
                pacienteSeleccionado = pacientesBD.First();

            if (pacienteSeleccionado == null) return NotFound("No hay pacientes registrados.");

            var vm = GenerarHistorialModel(año, mes, pacienteSeleccionado);

            ViewBag.Usuarios = pacientesBD.Select(p => new UsuarioResumen { Id = p.id, Nombre = p.nombre }).ToList();

            return View("HistorialMensual", vm);
        }

        [Authorize(Roles = "Paciente,paciente")]
        public IActionResult MiHistorial(int año = 2026, int mes = 5)
        {
            var perfilIdStr = User.FindFirst("PerfilId")?.Value;
            if (string.IsNullOrEmpty(perfilIdStr)) return RedirectToAction("Login", "Account");

            int idPaciente = int.Parse(perfilIdStr);

            var paciente = _context.Pacientes.FirstOrDefault(p => p.id == idPaciente);
            if (paciente == null) return NotFound();

            var vm = GenerarHistorialModel(año, mes, paciente);
            return View("HistorialMensual", vm);
        }

        private HistorialMensual GenerarHistorialModel(int año, int mes, Paciente paciente)
        {
            var primerDia = new DateTime(año, mes, 1);
            int diasVacios = (int)primerDia.DayOfWeek == 0 ? 6 : (int)primerDia.DayOfWeek - 1;
            int totalDias = DateTime.DaysInMonth(año, mes);

            var alertasDelMes = _context.Alertas
                .Where(a => a.paciente_id == paciente.id &&
                            a.fecha_hora.HasValue &&
                            a.fecha_hora.Value.Year == año &&
                            a.fecha_hora.Value.Month == mes)
                .ToList();

            var dias = new List<DiaHistorial>();
            int pulsoBaseEstable = 72;

            for (int diaCorriente = 1; diaCorriente <= totalDias; diaCorriente++)
            {
                var alertasDeEsteDia = alertasDelMes
                    .Where(a => a.fecha_hora.Value.Day == diaCorriente)
                    .ToList();

                bool tieneAlertaEseDia = alertasDeEsteDia.Any();

                int promedioPulso = tieneAlertaEseDia
                    ? (int)alertasDeEsteDia.Average(a => a.fc_media)
                    : pulsoBaseEstable;

                string mensaje = tieneAlertaEseDia
                    ? $"⚠️ {alertasDeEsteDia.Count} Alerta(s)"
                    : string.Empty;

                dias.Add(new DiaHistorial
                {
                    Numero = diaCorriente,
                    BpmPromedio = promedioPulso,
                    TieneAlerta = tieneAlertaEseDia,
                    MensajeAlerta = mensaje
                });
            }

            return new HistorialMensual
            {
                PeriodoNombre = primerDia.ToString("MMMM yyyy"),
                Mes = mes,
                Año = año,
                DiasVaciosInicio = diasVacios,
                PromedioMensual = dias.Any() ? (int)dias.Average(d => d.BpmPromedio) : pulsoBaseEstable,
                TotalIncidentes = alertasDelMes.Count,
                EstadoSalud = alertasDelMes.Count > 5 ? "Riesgo Moderado" : "Estable",
                DiasDelMes = dias,
                UsuarioId = paciente.id,
                UsuarioNombre = paciente.nombre
            };
        }

        [Authorize]
        public IActionResult ExportarPdf(int año = 2026, int mes = 5, int usuarioId = 1)
        {
            if (User.IsInRole("Paciente") || User.IsInRole("paciente"))
            {
                var claimId = User.FindFirst("PerfilId")?.Value;
                if (int.TryParse(claimId, out int pacienteId))
                    usuarioId = pacienteId;
                else
                    return Forbid();
            }
            return RedirectToAction("Index", "Reporte", new { año, mes, usuarioId });
        }
    }
}