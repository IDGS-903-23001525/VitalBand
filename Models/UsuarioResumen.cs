using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [NotMapped]
    public class UsuarioResumen
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Edad { get; set; }
        public string? Sexo { get; set; }
        public float? Peso { get; set; }
        public float? Altura { get; set; }
        public string? HistorialMedico { get; set; }
        public bool TieneAlertaHoy { get; set; }
        public int PulsoPromedioHoy { get; set; }
        public string? CedulaMedico { get; set; }
        public int? MedicoAsignadoId { get; set; }
    }
}