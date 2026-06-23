using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [NotMapped]
    public class AtenderAlerta
    {
        public int AlertaId { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }

        public int PulsoRegistrado { get; set; }
        public int DuracionSegundos { get; set; }
        public bool? Atendida { get; set; }
        public string RespuestaUsuario { get; set; } = string.Empty;
        public DateTime? FechaHoraAlerta { get; set; }
    }
}