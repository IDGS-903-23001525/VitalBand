using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VitalBand.Models
{
    [Table("MEDICOS")]
    public class Medico
    {
        [Key]
        public int id { get; set; }

        [Required]
        public int usuario_id { get; set; }

        [Required(ErrorMessage = "El nombre del médico es obligatorio.")]
        public string nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La especialidad es obligatoria.")]
        public string especialidad { get; set; } = string.Empty;

        [Required(ErrorMessage = "La cédula profesional es obligatoria.")]
        public string cedula_profesional { get; set; } = string.Empty;

        // Propiedad de navegación hacia la cuenta de usuario base (Relación 1:1)
        [ValidateNever]
        [ForeignKey("usuario_id")]
        public virtual Usuario Usuario { get; set; } = null!;
    }
}