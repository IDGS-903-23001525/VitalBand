using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using VitalBand.Data;
using VitalBand.Models;

namespace VitalBand.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfiguracionApiController : ControllerBase
    {
        private readonly VitalBandContext _context;

        public ConfiguracionApiController(VitalBandContext context)
        {
            _context = context;
        }

        // GET: api/ConfiguracionApi/paciente/5
        // Trae la configuración/datos médicos de un paciente específico
        [HttpGet("paciente/{id}")] // Cambié el nombre del parámetro a 'id' para que sea más claro
        public async Task<IActionResult> GetConfiguracionPaciente(int id)
        {
            // 🛠️ CORRECCIÓN: Buscamos por el 'id' del paciente e incluimos la relación con su Usuario base
            var paciente = await _context.Pacientes
                .Include(p => p.Usuario) // 👈 Indispensable para que no llegue el correo vacío a la web
                .FirstOrDefaultAsync(p => p.id == id);

            if (paciente == null)
            {
                return NotFound(new { mensaje = "Configuración de paciente no encontrada." });
            }

            return Ok(paciente);
        }

        // PUT: api/ConfiguracionApi/paciente/5
        [HttpPut("paciente/{id}")]
        public async Task<IActionResult> UpdateConfiguracion(int id, [FromBody] UsuarioResumen model)
        {
            if (id != model.Id) return BadRequest(new { mensaje = "El ID no coincide." });

            // 1. Buscamos al paciente en la base de datos
            var paciente = await _context.Pacientes.FindAsync(id);
            if (paciente == null) return NotFound(new { mensaje = "Paciente no encontrado." });

            // 2. Actualizamos sus datos médicos crudos
            paciente.nombre = model.Nombre;
            paciente.genero = model.Sexo;
            paciente.peso_inicial = model.Peso;
            paciente.altura_inicial = model.Altura;
            paciente.historial_medico_breve = model.HistorialMedico;

            // 3. También actualizamos su correo en la tabla USUARIOS relacionada
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.id == paciente.usuario_id);
            if (usuario != null)
            {
                usuario.email = model.Email;
            }

            _context.Entry(paciente).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Configuración y usuario actualizados correctamente." });
        }
        // GET: api/ConfiguracionApi/pacientes
        // Trae la lista completa de todos los pacientes en el sistema
        [HttpGet("pacientes")]
        public async Task<IActionResult> GetTodosLosPacientes()
        {
            var pacientes = await _context.Pacientes
                .Include(p => p.Usuario)
                .ToListAsync();

            return Ok(pacientes);
        }
    }
}