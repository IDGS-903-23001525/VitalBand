using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using VitalBand.Data;
using VitalBand.DTOs;
using VitalBand.Models;

namespace VitalBand.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class AlertasApiController : ControllerBase
    {
        private readonly VitalBandContext _context;

        public AlertasApiController(VitalBandContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAlertas()
        {
            var alerts = await _context.Alertas
                .Include(alert => alert.Paciente)
                .OrderByDescending(alert => alert.id)
                .ToListAsync();

            return Ok(alerts);
        }

        [HttpPut("atender/{id}")]
        public async Task<IActionResult> AttendAlert(int id)
        {
            var alert = await _context.Alertas.FindAsync(id);
            if (alert == null)
            {
                return NotFound(new { message = "La alerta no existe" });
            }

            alert.mensaje_enviado = true;
            await _context.SaveChangesAsync();

            await Middleware.WebSocketConnectionManager.SendMessageAsync(alert.paciente_id, "DISMISS");

            return Ok(new { message = "Alert processed successfully.", alertId = id });
        }

        [HttpPost("manual")]
        public async Task<IActionResult> RegisterManualAlert([FromBody] ManualAlertRequest request)
        {
            if (request == null || request.PatientId <= 0)
            {
                return BadRequest(new { message = "Se requiere un paciente valido." });
            }

            var alert = new Alerta
            {
                paciente_id = request.PatientId,
                fecha_hora = DateTime.UtcNow,
                mensaje_enviado = false
            };

            _context.Alertas.Add(alert);
            await _context.SaveChangesAsync();

            await Middleware.WebSocketConnectionManager.SendMessageAsync(alert.paciente_id, "ALERT");

            return Ok(new { message = "Alerta Manual registrada con éxito", alertId = alert.id });
        }
    }
}