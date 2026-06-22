using System.Collections.Generic;
using VitalBand.Models;

namespace VitalBand.Services
{
    public interface IConfiguracionService
    {
        // 👇 Cambiado de ObtenerRangos() a ObtenerRangosPulso() para coincidir con la vista y el controlador
        List<RangoPulsoConfig> ObtenerRangosPulso();

        List<TipoAlertaConfig> ObtenerTiposAlerta();

        void AgregarRango(RangoPulsoConfig rango);
        void AgregarTipoAlerta(TipoAlertaConfig tipo);

        void ActualizarRango(RangoPulsoConfig rango);
        void ActualizarTipoAlerta(TipoAlertaConfig tipo);
    }
}