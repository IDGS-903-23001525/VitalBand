using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using VitalBand.Data;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize(Roles = "Medico,medico")]
    public class DatosGeneralesController : Controller
    {
        private readonly VitalBandContext _context;

        public DatosGeneralesController(VitalBandContext context)
        {
            _context = context;
        }

        public IActionResult Index(int id)
        {
            var paciente = _context.Pacientes
                .Include(p => p.Usuario)
                .FirstOrDefault(p => p.id == id);

            if (paciente == null) return NotFound();

            int edadCalculada = DateTime.Today.Year - paciente.fecha_nacimiento.Year;
            if (DateTime.Today.DayOfYear < paciente.fecha_nacimiento.DayOfYear) edadCalculada--;

            int conteoAlertas = _context.Alertas.Count(a => a.paciente_id == paciente.id);

            var lecturas = GenerarLecturas(paciente.id);

            var modelo = new DatosGenerales
            {
                UsuarioId = paciente.id,
                Nombre = paciente.nombre,
                Edad = edadCalculada,
                Genero = paciente.genero ?? "No especificado",
                DescripcionMedica = paciente.historial_medico_breve ?? "Sin antecedentes registrados en el expediente.",
                FechaRegistro = paciente.Usuario?.fecha_registro ?? DateTime.Now,
                TotalAlertas = conteoAlertas,
                LecturasHoy = lecturas
            };

            ViewBag.HorasJson = System.Text.Json.JsonSerializer.Serialize(lecturas.Select(l => l.Hora.ToString("HH:mm")));
            ViewBag.PulsosJson = System.Text.Json.JsonSerializer.Serialize(lecturas.Select(l => l.Pulso));

            return View(modelo);
        }

        private List<LecturaDiaria> GenerarLecturas(int pacienteId)
        {
            var random = new Random(pacienteId);
            var ahora = DateTime.Now;
            var lecturas = new List<LecturaDiaria>();

            for (int i = 0; i < 24; i++)
            {
                int pulsoBase = random.Next(65, 85);
                lecturas.Add(new LecturaDiaria
                {
                    Hora = ahora.Date.AddHours(i),
                    Pulso = pulsoBase
                });
            }
            return lecturas;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActivarAlertaManual(int usuarioId, int pulso, string motivo)
        {
            var nuevaAlerta = new Alerta
            {
                paciente_id = usuarioId,
                fecha_hora = DateTime.Now,
                fc_media = pulso,
                hrv_rmssd = 25.0f,
                spo2_estabilidad = 95.0f,
                latitud = 21.1219f,
                longitud = -101.6825f,
                mensaje_enviado = false
            };

            _context.Alertas.Add(nuevaAlerta);
            _context.SaveChanges();

            TempData["Mensaje"] = $"⚠️ ¡Alerta médica manual guardada en MySQL para el paciente! Motivo: {motivo}";
            return RedirectToAction("Index", new { id = usuarioId });
        }
    }
}