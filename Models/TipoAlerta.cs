using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [NotMapped]
    public class TipoAlerta
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;   // "Taquicardia", "Bradicardia", etc.
        public int UmbralMinimo { get; set; }
        public int UmbralMaximo { get; set; }
        public string ColorHex { get; set; } = "#000000";
        public bool Activo { get; set; } = true;
    }
}