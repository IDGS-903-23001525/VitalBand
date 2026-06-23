using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [NotMapped]
    public class LecturaDiaria
    {
        public DateTime Hora { get; set; }
        public int Pulso { get; set; }
    }
}