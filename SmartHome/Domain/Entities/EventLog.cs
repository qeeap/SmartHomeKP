using System;
using SmartHome.Domain.Enums;

namespace SmartHome.Domain.Entities
{
    public class EventLog
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public EventType EventType { get; set; }
        public string Importance { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int? UserId { get; set; }
        public int? DeviceId { get; set; }
        public int? SensorId { get; set; }
        public int? ScenarioId { get; set; }
    }
}