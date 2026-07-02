namespace VitalBand.Models
{
    public class TelemetriaSaludDto
    {
        public string DeviceId { get; set; }
        public double Bpm { get; set; }
        public double Rmssd { get; set; }
        public double Spo2 { get; set; }
    }
}
