using VitalBand.Models;

namespace VitalBand.Services
{
    public interface IConfiguracionService
    {
        List<RangoPulsoConfig> ObtenerRangos();
        List<TipoAlertaConfig> ObtenerTiposAlerta();
        void AgregarRango(RangoPulsoConfig rango);
        void AgregarTipoAlerta(TipoAlertaConfig tipo);
        void ActualizarRango(RangoPulsoConfig rango);
        void ActualizarTipoAlerta(TipoAlertaConfig tipo);
    }
}