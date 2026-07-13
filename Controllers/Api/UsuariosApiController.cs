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
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.email == loginInfo.Email && u.password_hash == loginInfo.Password);

            if (usuario == null) return Unauthorized();

            string nuevoTokenSesion = Guid.NewGuid().ToString();

            usuario.token_sesion = nuevoTokenSesion;

            usuario.sesion_expiracion = DateTime.Now.AddMinutes(20);

            _context.Usuarios.Update(usuario);

            await _context.SaveChangesAsync();

            string nombreMostrar = "Usuario VitalBand";

            int perfilId = 0;

            if (usuario.rol.ToLower() == "medico")

            {

                var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.usuario_id == usuario.id);

                if (medico != null) { nombreMostrar = medico.nombre; perfilId = medico.id; }

            }
            else

            {
                var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.usuario_id == usuario.id);

                if (paciente != null) { nombreMostrar = paciente.nombre; perfilId = paciente.id; }

            }
            return Ok(new

            {
                id = usuario.id,

                email = usuario.email,

                rol = usuario.rol,

                nombreCompleto = nombreMostrar,

                perfilId = perfilId,

                tokenSesionActual = nuevoTokenSesion

            });

        }

        [HttpPost("logout/{id}")]

        public async Task<IActionResult> Logout(int id)

        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario != null)

            {

                usuario.token_sesion = null;

                usuario.sesion_expiracion = null;

                await _context.SaveChangesAsync();

            }

            return Ok();
        }

        [HttpPost("forgot-password")]

        public async Task<IActionResult> ForgotPassword([FromBody] string email)

        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.email == email);

            if (usuario == null) return NotFound();

            string tokenUrl = Guid.NewGuid().ToString();

            usuario.token_sesion = tokenUrl;

            usuario.sesion_expiracion = DateTime.Now.AddMinutes(15);

            await _context.SaveChangesAsync();

            return Ok(new { token = tokenUrl });

        }

        [HttpPost("reset-password")]

        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordViewModel model)

        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.email == model.Email && u.token_sesion == model.Token && u.sesion_expiracion > DateTime.Now);

            if (usuario == null) return BadRequest();

            usuario.password_hash = model.Password;

            usuario.token_sesion = null;

            usuario.sesion_expiracion = null;

            await _context.SaveChangesAsync();

            return Ok();

        }

        [HttpPost("registro-medico")]

        public async Task<IActionResult> RegistroMedico([FromBody] RegistroMedicoDTO model)

        {

            if (await _context.Usuarios.AnyAsync(u => u.email == model.Email)) return BadRequest("El correo ya existe.");

            if (await _context.Medicos.AnyAsync(m => m.cedula_profesional == model.Cedula)) return BadRequest("La cédula ya existe.");



            using (var transaction = await _context.Database.BeginTransactionAsync())

            {

                try

                {

                    var nuevoUsuario = new Usuario { email = model.Email, password_hash = model.Password, rol = "medico", fecha_registro = DateTime.Now };

                    _context.Usuarios.Add(nuevoUsuario);

                    await _context.SaveChangesAsync();



                    var nuevoMedico = new Medico { usuario_id = nuevoUsuario.id, nombre = model.Nombre, especialidad = model.Specialty, cedula_profesional = model.Cedula };

                    _context.Medicos.Add(nuevoMedico);

                    await _context.SaveChangesAsync();



                    await transaction.CommitAsync();

                    return Ok();

                }

                catch (Exception ex)

                {

                    await transaction.RollbackAsync();


                    return StatusCode(500, ex.InnerException?.Message ?? ex.Message);

                }

            }

        }

        [HttpGet("ObtenerUsuarioIdPorPaciente/{pacienteId}")]

        public async Task<IActionResult> ObtenerUsuarioIdPorPaciente(int pacienteId)

        {
            var paciente = await _context.Pacientes.FindAsync(pacienteId);

            if (paciente == null)

            {

                return NotFound(new { mensaje = "Paciente no encontrado." });

            }
            return Ok(new { usuarioIdReal = paciente.usuario_id });

        }

    }

}