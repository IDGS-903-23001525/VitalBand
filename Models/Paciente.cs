using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VitalBand.Models
{
    [Table("PACIENTES")]
    public class Paciente
    {
        [Key]
        public int id { get; set; }

        [Required]
        public int usuario_id { get; set; }

        public int? medico_asignado_id { get; set; }

        [Required(ErrorMessage = "El nombre del paciente es obligatorio.")]
        public string nombre { get; set; } = string.Empty;
        public string? genero { get; set; }
        [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
        [DataType(DataType.Date)]
        public DateTime fecha_nacimiento { get; set; }

        public string? tipo_sangre { get; set; }

        public float? peso_inicial { get; set; }

        public float? altura_inicial { get; set; }

        public string? historial_medico_breve { get; set; }

        

        // Relación 1:1 con la tabla de Usuarios base
        [ValidateNever]
        [ForeignKey("usuario_id")]
        public virtual Usuario Usuario { get; set; } = null!;

        // Relación 1:N opcional con Médicos (un paciente pertenece a un médico)
        [ValidateNever]
        [ForeignKey("medico_asignado_id")]
        public virtual Medico Medico { get; set; } = null!;
    }
}