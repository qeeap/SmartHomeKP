using System.Collections.Generic;

namespace SmartHome.Domain.Entities
{
    public class Room
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public int Floor { get; set; }
        public double Area { get; set; }

        public ICollection<Device> Devices { get; set; } = new List<Device>();
        public ICollection<Sensor> Sensors { get; set; } = new List<Sensor>();

        public void Rename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("Name cannot be empty");
            Name = newName;
        }
    }
}