using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VitalBand.Models
{
    [Table("PACIENTES_PATOLOGIAS")]
    public class PacientePatologia
    {
        [Key]
        public int id { get; set; }

        [Required]
        public int paciente_id { get; set; }

        [Required]
        public int patologia_id { get; set; }

        [ValidateNever]
        [ForeignKey("paciente_id")]
        public virtual Paciente Paciente { get; set; } = null!;

        [ValidateNever]
        [ForeignKey("patologia_id")]
        public virtual PatologiaCatalogo PatologiaCatalogo { get; set; } = null!;
    }
}
