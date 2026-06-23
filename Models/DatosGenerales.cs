using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [NotMapped]
    public class DatosGenerales
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Edad { get; set; }
        public string Genero { get; set; } = string.Empty;
        public string DescripcionMedica { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
        public int TotalAlertas { get; set; }
        public List<LecturaDiaria> LecturasHoy { get; set; } = new();
    }
}