using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [NotMapped]
    public class HistorialMensual
    {
        public string PeriodoNombre { get; set; } = "Mayo 2026";
        public int Mes { get; set; } = 5;
        public int Año { get; set; } = 2026;
        public int DiasVaciosInicio { get; set; } = 4;
        public int PromedioMensual { get; set; } = 74;
        public int TotalIncidentes { get; set; } = 1;
        public string EstadoSalud { get; set; } = "Estable";
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; } = string.Empty;
        public List<double> DatosGrafica { get; set; } = new List<double>();

        public List<DiaHistorial> DiasDelMes { get; set; } = new();
    }

    [NotMapped]
    public class DiaHistorial
    {
        public int Numero { get; set; }
        public int BpmPromedio { get; set; }
        public bool TieneAlerta { get; set; }
        public string? MensajeAlerta { get; set; }
    }
}