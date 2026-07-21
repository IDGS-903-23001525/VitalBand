namespace VitalBand.DTOs
{
    public sealed class ManualAlertRequest
    {
        public int PatientId { get; set; }
        public float? FcMedia { get; set; }
        public float? HrvRmssd { get; set; }
        public float? Spo2Estabilidad { get; set; }
        public float? Latitud { get; set; }
        public float? Longitud { get; set; }
    }
}
