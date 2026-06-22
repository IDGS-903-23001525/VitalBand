using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VitalBand.Models;

namespace VitalBand.Controllers
{
    [Authorize(Roles = "Medico")]
    public class UsuariosController : Controller
    {
        public IActionResult Index()
        {
            var usuarios = ObtenerUsuarios();
            var usuariosOrdenados = usuarios.OrderByDescending(u => u.TieneAlertaHoy).ToList();
            return View(usuariosOrdenados);
        }

        // Método estático para ser usado por otros controladores
        public static List<UsuarioResumenViewModel> ObtenerUsuarios()
        {
            return new List<UsuarioResumenViewModel>
            {
                new() { Id = 1, Nombre = "Paulina Vargas", Edad = 21, Sexo = "Femenino", PulsoPromedioHoy = 74, TieneAlertaHoy = false, Email = "paciente1@vitalband.com" },
                new() { Id = 2, Nombre = "Carlos Méndez", Edad = 45, Sexo = "Masculino", PulsoPromedioHoy = 82, TieneAlertaHoy = true, Email = "paciente@vitalband.com" },
                new() { Id = 3, Nombre = "Laura Jiménez", Edad = 33, Sexo = "Femenino", PulsoPromedioHoy = 68, TieneAlertaHoy = false, Email = "paciente3@vitalband.com" }
            };
        }
    }
}