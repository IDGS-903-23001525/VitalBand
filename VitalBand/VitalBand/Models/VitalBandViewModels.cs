using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VitalBand.Models
{
    // ─── Login ───────────────────────────────────────────────────────────────
    public class LoginViewModel
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        [Display(Name = "Correo electrónico")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Mantener sesión iniciada")]
        public bool RememberMe { get; set; }
    }

    // ─── Historial Mensual ────────────────────────────────────────────────────
    public class DiaHistorial
    {
        public int Numero { get; set; }
        public int BpmPromedio { get; set; }
        public bool TieneAlerta { get; set; }
        public string? MensajeAlerta { get; set; }
    }

    public class HistorialMensualViewModel
    {
        public string PeriodoNombre { get; set; } = "Mayo 2026";
        public int Mes { get; set; } = 5;
        public int Año { get; set; } = 2026;

        /// <summary>Cuántas celdas vacías agregar antes del día 1 (0 = lunes).</summary>
        public int DiasVaciosInicio { get; set; } = 4;  // Mayo 2026 empieza en viernes

        public int PromedioMensual { get; set; } = 74;
        public int TotalIncidentes { get; set; } = 1;
        public string EstadoSalud { get; set; } = "Estable";
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; } = string.Empty;

        public List<DiaHistorial> DiasDelMes { get; set; } = new();
    }

    // ─── Reporte de Salud ─────────────────────────────────────────────────────
    public class IncidenteCritico
    {
        public DateTime FechaHora { get; set; }
        public string Descripcion { get; set; } = string.Empty;

        /// <summary>"high" | "low" | "irregular" — controla el color del badge.</summary>
        public string Tipo { get; set; } = "high";
    }

    public class ReporteSaludViewModel
    {
        public string NombrePaciente { get; set; } = string.Empty;
        public int EdadPaciente { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public string Identificador { get; set; } = string.Empty;
        public List<IncidenteCritico> Incidentes { get; set; } = new();
    }

    // ========================== VISTA USUARIOS ==========================
    public class UsuarioResumenViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Edad { get; set; }
        public string Sexo { get; set; } = string.Empty;   // "Masculino", "Femenino", "Otro"
        public int PulsoPromedioHoy { get; set; }          // BPM
        public bool TieneAlertaHoy { get; set; }           // si tuvo alerta hoy
        public string? AlertaMensaje { get; set; }         // mensaje corto de la alerta (si aplica)
        public string Email { get; set; } = string.Empty;
    }

    // ========================== VISTA DATOS GENERALES ==========================
    public class LecturaDiariaViewModel
    {
        public DateTime Hora { get; set; }
        public int Pulso { get; set; }
    }

    public class DatosGeneralesViewModel
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Edad { get; set; }
        public string Genero { get; set; } = string.Empty;
        public string DescripcionMedica { get; set; } = string.Empty;   // problemas del paciente
        public DateTime FechaRegistro { get; set; }
        public int TotalAlertas { get; set; }

        // Datos para gráfica del día de hoy
        public List<LecturaDiariaViewModel> LecturasHoy { get; set; } = new();
    }

    // ========================== VISTA HISTORIAL ALERTAS ==========================
    public class AlertaHistorialViewModel
    {
        public int Id { get; set; }
        public DateTime FechaHora { get; set; }
        public string Ubicacion { get; set; } = string.Empty;   // "Lat,Long" o dirección
        public bool Respondida { get; set; }
        public string DescripcionEvento { get; set; } = string.Empty;
    }

    // ========================== VISTA ATENDER ALERTA ==========================
    public class AtenderAlertaViewModel
    {
        public int AlertaId { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public int PulsoRegistrado { get; set; }
        public int DuracionSegundos { get; set; }   // cuánto tiempo duró el pulso alto/bajo
        public bool Atendida { get; set; }
        public string RespuestaUsuario { get; set; } = string.Empty;   // lo que respondió el paciente
        public DateTime FechaHoraAlerta { get; set; }
    }

    // ========================== CONFIGURACIÓN ==========================
    public class RangoPulsoConfig
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;  // "Bajo", "Normal", "Alto", "Crítico"
        public int Minimo { get; set; }
        public int Maximo { get; set; }  // Si Maximo = -1 significa infinito
        public string ColorHex { get; set; } = "#000000";
    }

    public class TipoAlertaConfig
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;   // "Taquicardia", "Bradicardia", etc.
        public int UmbralMinimo { get; set; }
        public int UmbralMaximo { get; set; }
        public string ColorHex { get; set; } = "#000000";
        public bool Activo { get; set; } = true;
    }

    public class ConfiguracionViewModel
    {
        public List<RangoPulsoConfig> RangosPulso { get; set; } = new();
        public List<TipoAlertaConfig> TiposAlerta { get; set; } = new();
        public RangoPulsoConfig NuevoRango { get; set; } = new();
        public TipoAlertaConfig NuevoTipoAlerta { get; set; } = new();
    }
}
