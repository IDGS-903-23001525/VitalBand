using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [NotMapped]
    public class ReporteSalud
    {
        public string NombrePaciente { get; set; } = string.Empty;
        public int EdadPaciente { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public string Identificador { get; set; } = string.Empty;
        public List<IncidenteCritico> Incidentes { get; set; } = new();
    }

    [NotMapped]
    public class IncidenteCritico
    {
        public DateTime FechaHora { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string Tipo { get; set; } = "high"; // high, low, irregular
    }
}