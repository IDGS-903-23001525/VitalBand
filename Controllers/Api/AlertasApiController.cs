using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using VitalBand.Data;
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

        // GET: api/AlertasApi
        // Trae todas las alertas con los datos del paciente incluidos para tu vista de historial
        [HttpGet]
        public async Task<IActionResult> GetAlertas()
        {
            var alertas = await _context.Alertas
                .Include(a => a.Paciente) // Carga los datos de la relación 
                .OrderByDescending(a => a.id) // Ordena por tu llave primaria 'id' en minúscula
                .ToListAsync();

            return Ok(alertas);
        }

        // PUT: api/AlertasApi/atender/5
        // Actualiza el estado del mensaje/alerta usando tu propiedad 'id'
        [HttpPut("atender/{id}")]
        public async Task<IActionResult> AtenderAlerta(int id)
        {
            var alerta = await _context.Alertas.FindAsync(id);
            if (alerta == null)
            {
                return NotFound(new { mensaje = "La alerta no existe." });
            }

            // Cambiamos el estado del flujo usando tu propiedad 'mensaje_enviado'
            alerta.mensaje_enviado = true;

            _context.Entry(alerta).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            await Middleware.WebSocketConnectionManager.SendMessageAsync(alerta.paciente_id, "DISMISS");

            return Ok(new { mensaje = "Alerta procesada correctamente y apagado remoto enviado.", alertaId = id });
        }

        // POST: api/AlertasApi/manual
        [HttpPost("manual")]
        public async Task<IActionResult> RegistrarAlertaManual([FromBody] Alerta nuevaAlerta)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (nuevaAlerta.Paciente != null)
            {
                _context.Entry(nuevaAlerta.Paciente).State = EntityState.Unchanged;
            }

            _context.Alertas.Add(nuevaAlerta);
            await _context.SaveChangesAsync();

            await Middleware.WebSocketConnectionManager.SendMessageAsync(nuevaAlerta.paciente_id, "ALERT");

            return Ok(new { mensaje = "Alerta manual registrada exitosamente y distribuida por WebSocket.", id = nuevaAlerta.id });
        }
    }
}