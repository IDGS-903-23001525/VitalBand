using System;
using System.Collections.Generic;

namespace VitalBand.DTOs
{
    public class RegistroPacienteDTO
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Sexo { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        public string? TipoSangre { get; set; }
        public double Peso { get; set; }
        public double Altura { get; set; }
        public string? HistorialMedico { get; set; }

        public string NombreContacto { get; set; } = string.Empty;
        public string ParentescoContacto { get; set; } = string.Empty;
        public string TelefonoContacto { get; set; } = string.Empty;

        public string? CedulaMedico { get; set; }
        public int? MedicoAsignadoId { get; set; }

        public List<int> PatologiasIds { get; set; } = new();
    }
}
