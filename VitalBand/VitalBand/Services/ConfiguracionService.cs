using VitalBand.Models;

namespace VitalBand.Services
{
    public class ConfiguracionService : IConfiguracionService
    {
        private List<RangoPulsoConfig> _rangos;
        private List<TipoAlertaConfig> _tiposAlerta;

        public ConfiguracionService()
        {
            // Datos iniciales
            _rangos = new List<RangoPulsoConfig>
            {
                new() { Id = 1, Nombre = "Crítico bajo", Minimo = 0, Maximo = 49, ColorHex = "#D32F2F" },
                new() { Id = 2, Nombre = "Precaución bajo", Minimo = 50, Maximo = 59, ColorHex = "#F4A100" },
                new() { Id = 3, Nombre = "Normal", Minimo = 60, Maximo = 100, ColorHex = "#2E7D32" },
                new() { Id = 4, Nombre = "Precaución alto", Minimo = 101, Maximo = 110, ColorHex = "#F4A100" },
                new() { Id = 5, Nombre = "Crítico alto", Minimo = 111, Maximo = 200, ColorHex = "#D32F2F" }
            };
            _tiposAlerta = new List<TipoAlertaConfig>
            {
                new() { Id = 1, Nombre = "Taquicardia", UmbralMinimo = 100, UmbralMaximo = 200, ColorHex = "#D32F2F", Activo = true },
                new() { Id = 2, Nombre = "Bradicardia", UmbralMinimo = 0, UmbralMaximo = 55, ColorHex = "#F4A100", Activo = true },
                new() { Id = 3, Nombre = "Arritmia", UmbralMinimo = 0, UmbralMaximo = 200, ColorHex = "#9C27B0", Activo = true }
            };
        }

        public List<RangoPulsoConfig> ObtenerRangos() => _rangos.OrderBy(r => r.Minimo).ToList();
        public List<TipoAlertaConfig> ObtenerTiposAlerta() => _tiposAlerta;

        public void AgregarRango(RangoPulsoConfig rango)
        {
            rango.Id = _rangos.Max(r => r.Id) + 1;
            _rangos.Add(rango);
        }

        public void AgregarTipoAlerta(TipoAlertaConfig tipo)
        {
            tipo.Id = _tiposAlerta.Max(t => t.Id) + 1;
            _tiposAlerta.Add(tipo);
        }

        public void ActualizarRango(RangoPulsoConfig rango)
        {
            var existente = _rangos.FirstOrDefault(r => r.Id == rango.Id);
            if (existente != null)
            {
                existente.Nombre = rango.Nombre;
                existente.Minimo = rango.Minimo;
                existente.Maximo = rango.Maximo;
                existente.ColorHex = rango.ColorHex;
            }
        }

        public void ActualizarTipoAlerta(TipoAlertaConfig tipo)
        {
            var existente = _tiposAlerta.FirstOrDefault(t => t.Id == tipo.Id);
            if (existente != null)
            {
                existente.Nombre = tipo.Nombre;
                existente.UmbralMinimo = tipo.UmbralMinimo;
                existente.UmbralMaximo = tipo.UmbralMaximo;
                existente.ColorHex = tipo.ColorHex;
                existente.Activo = tipo.Activo;
            }
        }
    }
}