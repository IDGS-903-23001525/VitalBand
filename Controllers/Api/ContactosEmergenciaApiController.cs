using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using VitalBand.Data;
using VitalBand.Models;

namespace VitalBand.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactosEmergenciaApiController : ControllerBase
    {
        private readonly VitalBandContext _context;

        public ContactosEmergenciaApiController(VitalBandContext context)
        {
            _context = context;
        }

        [HttpGet("paciente/{pacienteId}")]
        public async Task<IActionResult> GetContactosPorPaciente(int pacienteId)
        {
            var contactos = await _context.ContactosEmergencia
                .Where(c => c.paciente_id == pacienteId)
                .OrderBy(c => c.prioridad)
                .ToListAsync();

            return Ok(contactos);
        }

        [HttpPost]
        public async Task<IActionResult> AgregarContacto([FromBody] ContactoEmergencia nuevoContacto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            _context.ContactosEmergencia.Add(nuevoContacto);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Contacto de emergencia agregado con exito.", id = nuevoContacto.id });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarContacto(int id)
        {
            var contacto = await _context.ContactosEmergencia.FindAsync(id);
            if (contacto == null) return NotFound();

            _context.ContactosEmergencia.Remove(contacto);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Contacto eliminado correctamente." });
        }
    }
}
