using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [Table("USUARIOS")]
    public class Usuario
    {
        [Key]
        public int id { get; set; }

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingresa un formato de correo válido.")]
        public string email { get; set; }

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string password_hash { get; set; }

        public string rol { get; set; } // 'paciente' o 'medico'
        public DateTime? fecha_registro { get; set; } = DateTime.Now;
        public string? token_sesion { get; set; }
        public DateTime? sesion_expiracion { get; set; }
    }
}