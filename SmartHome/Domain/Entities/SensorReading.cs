using System;

namespace SmartHome.Domain.Entities
{
    public class SensorReading
    {
        public int Id { get; set; }
        public int SensorId { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}