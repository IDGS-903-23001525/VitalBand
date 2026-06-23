using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [NotMapped]
    public class RangoPulso
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;  // "Bajo", "Normal", "Alto", "Crítico"
        public int Minimo { get; set; }
        public int Maximo { get; set; }
        public string ColorHex { get; set; } = "#000000";
    }
}