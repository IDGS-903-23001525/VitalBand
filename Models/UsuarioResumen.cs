using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [NotMapped]
    public class UsuarioResumen
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Edad { get; set; }
        public string? Sexo { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string? TipoSangre { get; set; }
        public float? Peso { get; set; }
        public float? Altura { get; set; }
        public string? HistorialMedico { get; set; }
        public bool TieneAlertaHoy { get; set; }
        public int PulsoPromedioHoy { get; set; }
        public string? CedulaMedico { get; set; }
        public int? MedicoAsignadoId { get; set; }
        public int AlertaIdPendiente { get; set; }

        public string NombreContacto { get; set; } = string.Empty;
        public string ParentescoContacto { get; set; } = string.Empty;
        public string TelefonoContacto { get; set; } = string.Empty;
        public List<int> PatologiasIds { get; set; } = new();
    }
}