using SmartHome.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHome.Domain.Entities
{
    public class Sensor
    {
        public int Id { get; set; }
        public int? OwnerId { get; set; }
        public int? RoomId { get; set; }
        public string Name { get; set; } = string.Empty;
        public SensorType Type { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime InstalledAt { get; set; } = DateTime.UtcNow;


        public Room? Room { get; set; }

        public ICollection<SensorReading> Readings { get; set; } = new List<SensorReading>();

        public void Activate() => IsActive = true;
        public void Deactivate() => IsActive = false;


    }
}
