using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [NotMapped]
    public class AlertaHistorial
    {
        public int Id { get; set; }
        public DateTime FechaHora { get; set; }
        public string Ubicacion { get; set; } = string.Empty;
        public bool Respondida { get; set; }
        public string DescripcionEvento { get; set; } = string.Empty;
    }
}