using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using VitalBand.Data;
using VitalBand.Models;

namespace VitalBand.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuariosApiController : ControllerBase
    {
        private readonly VitalBandContext _context;

        public UsuariosApiController(VitalBandContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Login loginInfo)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // 1. Validamos las credenciales en la tabla USUARIOS
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.email == loginInfo.Email && u.password_hash == loginInfo.Password);

            if (usuario == null)
            {
                return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
            }

            // 2. Buscamos su información de perfil según su rol
            string nombreMostrar = "Usuario VitalBand";
            int perfilId = 0;

            if (usuario.rol == "medico")
            {
                var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.usuario_id == usuario.id);
                if (medico != null)
                {
                    nombreMostrar = medico.nombre;
                    perfilId = medico.id;
                }
            }
            else if (usuario.rol == "paciente")
            {
                var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.usuario_id == usuario.id);
                if (paciente != null)
                {
                    nombreMostrar = paciente.nombre;
                    perfilId = paciente.id;
                }
            }

            // 3. Devolvemos una respuesta armada con TODO lo que necesita la Web y la Móvil
            return Ok(new
            {
                id = usuario.id,
                email = usuario.email,
                rol = usuario.rol,
                nombreCompleto = nombreMostrar,
                perfilId = perfilId
            });
        }
    }
}