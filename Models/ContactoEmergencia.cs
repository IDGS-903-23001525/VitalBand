using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VitalBand.Models
{
    [Table("CONTACTOS_EMERGENCIA")]
    public class ContactoEmergencia
    {
        [Key]
        public int id { get; set; }

        [Required]
        public int paciente_id { get; set; }

        [Required]
        [StringLength(150)]
        public string nombre { get; set; } = string.Empty;

        [StringLength(50)]
        public string? parentesco { get; set; }

        [Required]
        [StringLength(20)]
        public string telefono { get; set; } = string.Empty;

        public int prioridad { get; set; } = 1;

        [ValidateNever]
        [ForeignKey("paciente_id")]
        public virtual Paciente Paciente { get; set; } = null!;
    }
}
