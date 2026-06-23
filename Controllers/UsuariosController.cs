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
            DateTime hoy = DateTime.Today;

            var usuariosResumen = _context.Pacientes
                .Include(p => p.Usuario)
                .ToList() 
                .Select(p => {
                    var alertasHoy = _context.Alertas
                        .Where(a => a.paciente_id == p.id && a.fecha_hora >= hoy)
                        .ToList();

                    return new UsuarioResumen
                    {
                        Id = p.id,
                        Nombre = p.nombre,
                        Email = p.Usuario?.email ?? string.Empty,

                        Edad = DateTime.Today.Year - p.fecha_nacimiento.Year -
                               (DateTime.Today.DayOfYear < p.fecha_nacimiento.DayOfYear ? 1 : 0),

                        Sexo = p.genero ?? "No Especificado",

                        TieneAlertaHoy = alertasHoy.Any(),

                        PulsoPromedioHoy = alertasHoy.Any() ? (int)alertasHoy.Average(a => a.fc_media) : 70
                    };
                })
                .ToList();

            var usuariosOrdenados = usuariosResumen.OrderByDescending(u => u.TieneAlertaHoy).ToList();

            return View(usuariosOrdenados);
        }
    }
}