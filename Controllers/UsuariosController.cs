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

            // 1. Jalamos los pacientes de la base de datos
            var usuariosResumen = _context.Pacientes
                .Include(p => p.Usuario)
                .ToList() // <-- Al traerlos a memoria con ToList, podemos meter lógica compleja de C# sin que MySQL se queje
                .Select(p => {
                    // Calculamos las alertas de hoy en memoria de forma segura
                    var alertasHoy = _context.Alertas
                        .Where(a => a.paciente_id == p.id && a.fecha_hora >= hoy)
                        .ToList();

                    return new UsuarioResumenViewModel
                    {
                        Id = p.id,
                        Nombre = p.nombre,
                        Email = p.Usuario?.email ?? string.Empty,

                        // Cálculo de Edad seguro
                        Edad = DateTime.Today.Year - p.fecha_nacimiento.Year -
                               (DateTime.Today.DayOfYear < p.fecha_nacimiento.DayOfYear ? 1 : 0),

                        Sexo = "No Especificado",

                        // IoT Real
                        TieneAlertaHoy = alertasHoy.Any(),

                        // Si hay alertas hoy sacamos el promedio, si no, dejamos 70 base
                        PulsoPromedioHoy = alertasHoy.Any() ? (int)alertasHoy.Average(a => a.fc_media) : 70
                    };
                })
                .ToList();

            // 2. Ordenamos para priorizar urgencias
            var usuariosOrdenados = usuariosResumen.OrderByDescending(u => u.TieneAlertaHoy).ToList();

            return View(usuariosOrdenados);
        }

        // =========================================================================
        // 👇 RESPALDO: Dejamos este método para que los componentes de tus compañeros compilen
        // =========================================================================
        public static List<UsuarioResumenViewModel> ObtenerUsuarios()
        {
            return new List<UsuarioResumenViewModel>
            {
                new() { Id = 1, Nombre = "Paulina Vargas", Edad = 21, Sexo = "Femenino", PulsoPromedioHoy = 74, TieneAlertaHoy = false, Email = "paciente1@vitalband.com" },
                new() { Id = 2, Nombre = "Carlos Mendoza", Edad = 45, Sexo = "Masculino", PulsoPromedioHoy = 82, TieneAlertaHoy = true, Email = "paciente@vitalband.com" },
                new() { Id = 3, Nombre = "Laura Jiménez", Edad = 33, Sexo = "Femenino", PulsoPromedioHoy = 68, TieneAlertaHoy = false, Email = "paciente3@vitalband.com" }
            };
        }
    }
}