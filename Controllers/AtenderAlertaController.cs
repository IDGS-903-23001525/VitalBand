using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using VitalBand.Data;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize(Roles = "medico,Medico")]
    public class AtenderAlertaController : Controller
    {
        private readonly VitalBandContext _context;

        public AtenderAlertaController(VitalBandContext context)
        {
            _context = context;
        }

        public IActionResult Index(int alertaId)
        {
            var alertaBD = _context.Alertas.FirstOrDefault(a => a.id == alertaId);

            if (alertaBD == null)
            {
                TempData["Error"] = "No se encontró el registro de la alerta biométrica.";
                return RedirectToAction("Index", "Usuarios");
            }

            var modelo = new AtenderAlerta
            {
                AlertaId = alertaBD.id,
                Latitud = alertaBD.latitud,               
                Longitud = alertaBD.longitud,             
                PulsoRegistrado = (int)alertaBD.fc_media, 
                DuracionSegundos = 45,                    
                Atendida = alertaBD.mensaje_enviado,      
                RespuestaUsuario = "Alerta crítica disparada por el dispositivo móvil.",
                FechaHoraAlerta = alertaBD.fecha_hora
            };

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GuardarSeguimiento(AtenderAlerta model)
        {
            var alertaBD = _context.Alertas.FirstOrDefault(a => a.id == model.AlertaId);

            if (alertaBD != null)
            {
                alertaBD.mensaje_enviado = true;

                _context.SaveChanges();
                TempData["Mensaje"] = "¡La alerta médica ha sido atendida y registrada con éxito! ❤️✨";
            }

            return RedirectToAction("Index", "Usuarios");
        }
    }
}