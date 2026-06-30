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
    [Authorize(Roles = "medico,Medico")]
    public class UsuariosController : Controller
    {
        private readonly VitalBandContext _context;

        public UsuariosController(VitalBandContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(usuarioIdClaim) || !int.TryParse(usuarioIdClaim, out int usuarioId))
            {
                return RedirectToAction("Login", "Account");
            }

            // 2. Buscar al médico en la BD usando directamente el ID de usuario extraído
            var medico = _context.Medicos
                .AsNoTracking()
                .FirstOrDefault(m => m.usuario_id == usuarioId);

            if (medico == null)
            {
                return Forbid();
            }

            DateTime hoy = DateTime.Today;

            var pacientes = _context.Pacientes
                .Include(p => p.Usuario)
                .AsNoTracking() // 👈 Súper importante: Rompe el bucle de actualizaciones automáticas
                .Where(p => p.medico_asignado_id == medico.id)
                .ToList();

            // Obtenemos los IDs de los pacientes para traer todas las alertas de un solo golpe
            var pacienteIds = pacientes.Select(p => p.id).ToList();

            var alertasDeHoy = _context.Alertas
                .AsNoTracking()
                .Where(a => pacienteIds.Contains(a.paciente_id) && a.fecha_hora >= hoy)
                .ToList();

            // 4. Mapeamos al modelo final en memoria pura sin tocar la base de datos
            var usuariosResumen = pacientes.Select(p => {
                var alertasPaciente = alertasDeHoy.Where(a => a.paciente_id == p.id).ToList();

                return new UsuarioResumen
                {
                    Id = p.id,
                    Nombre = p.nombre,
                    Email = p.Usuario?.email ?? string.Empty,

                    Edad = DateTime.Today.Year - p.fecha_nacimiento.Year -
                           (DateTime.Today.DayOfYear < p.fecha_nacimiento.DayOfYear ? 1 : 0),

                    Sexo = p.genero ?? "No Especificado",

                    TieneAlertaHoy = alertasPaciente.Any(),

                    PulsoPromedioHoy = alertasPaciente.Any() ? (int)alertasPaciente.Average(a => a.fc_media) : 70
                };
            }).ToList();

            var usuariosOrdenados = usuariosResumen.OrderByDescending(u => u.TieneAlertaHoy).ToList();

            return View(usuariosOrdenados);
        }
    }
}