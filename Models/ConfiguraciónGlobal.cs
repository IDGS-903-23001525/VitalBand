using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [NotMapped]
    public class ConfiguracionGlobal
    {
        public List<RangoPulso> RangosPulso { get; set; } = new();
        public List<TipoAlerta> TiposAlerta { get; set; } = new();
        public RangoPulso NuevoRango { get; set; } = new();
        public TipoAlerta NuevoTipoAlerta { get; set; } = new();
    }
}