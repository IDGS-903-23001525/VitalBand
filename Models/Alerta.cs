using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VitalBand.Models
{
    [Table("ALERTAS")]
    public class Alerta
    {
        [Key]
        public int id { get; set; }
        public int paciente_id { get; set; }
        public DateTime? fecha_hora { get; set; } = DateTime.Now;
        public float fc_media { get; set; }
        public float hrv_rmssd { get; set; }
        public float spo2_estabilidad { get; set; }
        public float? latitud { get; set; }
        public float? longitud { get; set; }
        public bool? mensaje_enviado { get; set; } = false;

        [ValidateNever]
        [ForeignKey("paciente_id")]
        public virtual Paciente Paciente { get; set; } = null!;
    }
}