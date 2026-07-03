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
    public class ConfiguracionApiController : ControllerBase
    {
        private readonly VitalBandContext _context;

        public ConfiguracionApiController(VitalBandContext context)
        {
            _context = context;
        }

        // GET: api/ConfiguracionApi/paciente/5
        [HttpGet("paciente/{id}")]
        public async Task<IActionResult> GetConfiguracionPaciente(int id)
        {
            var paciente = await _context.Pacientes
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.id == id);

            if (paciente == null) return NotFound();

            // Si tiene un médico asignado, podemos inyectarle la Cédula al vuelo en una propiedad dinámica o DTO si lo prefieres,
            // pero para mantener tu Paciente plano, la web se encargará de pedirla o la API la incluye si creas un objeto anónimo:
            string? cedulaActual = null;
            if (paciente.medico_asignado_id.HasValue)
            {
                var mId = paciente.medico_asignado_id.Value;
                cedulaActual = await _context.Medicos
                    .Where(m => m.id == mId)
                    .Select(m => m.cedula_profesional)
                    .FirstOrDefaultAsync();
            }

            return Ok(new { paciente, cedulaActual, usuarioEmail = paciente.Usuario?.email });
        }

        // GET: api/ConfiguracionApi/verificar-cedula?cedula=XYZ
        [HttpGet("verificar-cedula")]
        public async Task<IActionResult> VerificarCedula(string cedula)
        {
            if (string.IsNullOrEmpty(cedula)) return BadRequest();

            var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.cedula_profesional == cedula.Trim());
            if (medico != null)
            {
                return Ok(new { existe = true, id = medico.id, nombre = medico.nombre });
            }
            return Ok(new { existe = false, mensaje = "Médico no encontrado en VitalBand ❌" });
        }

        // PUT: api/ConfiguracionApi/paciente/5
        [HttpPut("paciente/{id}")]
        public async Task<IActionResult> UpdateConfiguracion(int id, [FromBody] UsuarioResumen model)
        {
            var paciente = await _context.Pacientes.FindAsync(id);
            if (paciente == null) return NotFound();

            paciente.nombre = model.Nombre;
            paciente.genero = model.Sexo;
            paciente.peso_inicial = model.Peso;
            paciente.altura_inicial = model.Altura;
            paciente.historial_medico_breve = model.HistorialMedico;
            paciente.medico_asignado_id = model.MedicoAsignadoId; // Vinculación unificada del médico

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.id == paciente.usuario_id);
            if (usuario != null) usuario.email = model.Email;

            await _context.SaveChangesAsync();
            return Ok();
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

        // POST: api/ConfiguracionApi/registro-paciente
        [HttpPost("registro-paciente")]
        public async Task<IActionResult> RegistroPaciente([FromBody] RegistroPacienteDTO model)
        {
            if (await _context.Usuarios.AnyAsync(u => u.email == model.Email))
                return BadRequest("El correo electronico ya se encuentra registrado.");

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var nuevoUsuario = new Usuario
                    {
                        email = model.Email,
                        password_hash = model.Password,
                        rol = "paciente",
                        fecha_registro = DateTime.Now
                    };
                    _context.Usuarios.Add(nuevoUsuario);
                    await _context.SaveChangesAsync();

                    var nuevoPaciente = new Paciente
                    {
                        usuario_id = nuevoUsuario.id,
                        nombre = model.Nombre,
                        genero = model.Sexo,
                        fecha_nacimiento = model.FechaNacimiento,
                        tipo_sangre = model.TipoSangre,
                        peso_inicial = (float)model.Peso,
                        altura_inicial = (float)model.Altura,
                        historial_medico_breve = model.HistorialMedico
                    };
                    _context.Pacientes.Add(nuevoPaciente);
                    await _context.SaveChangesAsync();

                    var contactoEmergencia = new ContactoEmergencia
                    {
                        paciente_id = nuevoPaciente.id,
                        nombre = model.NombreContacto,
                        parentesco = model.ParentescoContacto,
                        telefono = model.TelefonoContacto,
                        prioridad = 1
                    };
                    _context.ContactosEmergencia.Add(contactoEmergencia);

                    if (model.PatologiasIds != null && model.PatologiasIds.Count > 0)
                    {
                        foreach (var patologiaId in model.PatologiasIds)
                        {
                            var pacientePatologia = new PacientePatologia
                            {
                                paciente_id = nuevoPaciente.id,
                                patologia_id = patologiaId
                            };
                            _context.PacientesPatologias.Add(pacientePatologia);
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new { mensaje = "Onboarding completado con exito." });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, $"Error interno: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
        }
    }
}