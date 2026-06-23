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
    public class HistorialAlertasController : Controller
    {
        private readonly VitalBandContext _context;

        public HistorialAlertasController(VitalBandContext context)
        {
            _context = context;
        }
        public IActionResult Index(int usuarioId)
        {
            var alertasBD = _context.Alertas
                .Where(a => a.paciente_id == usuarioId)
                .OrderByDescending(a => a.fecha_hora)
                .ToList();

            var modeloPlano = alertasBD.Select(a => new AlertaHistorial
            {
                Id = a.id,
                FechaHora = a.fecha_hora ?? DateTime.Now,
                Ubicacion = $"Lat: {a.latitud}, Long: {a.longitud}",
                Respondida = a.mensaje_enviado ?? false,
                DescripcionEvento = $"Alerta registrada con {a.fc_media} BPM. Estabilidad: SpO2 al {a.spo2_estabilidad}% y HRV de {a.hrv_rmssd}."
            }).ToList();

            ViewBag.UsuarioId = usuarioId;

            return View(modeloPlano);
        }
    }
}