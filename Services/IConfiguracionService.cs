using System.Collections.Generic;
using VitalBand.Models;

namespace VitalBand.Services
{
    public interface IConfiguracionService
    {
        List<RangoPulso> ObtenerRangosPulso();
        List<TipoAlerta> ObtenerTiposAlerta();

        void AgregarRango(RangoPulso rango);
        void AgregarTipoAlerta(TipoAlerta tipo);

        void ActualizarRango(RangoPulso rango);
        void ActualizarTipoAlerta(TipoAlerta tipo);
    }
}