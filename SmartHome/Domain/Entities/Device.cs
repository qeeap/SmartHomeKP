using SmartHome.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHome.Domain.Entities
{
    public class Device
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public int? RoomId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DeviceStatus Status { get; set; } = DeviceStatus.Inactive;
        public bool PowerState { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsOnline { get; set; }
        public DateTime UpdateAt { get; set; } = DateTime.UtcNow;

        public Room ? Room { get; set; }

        public void AssignToRoom(int roomId)
        {
            RoomId = roomId;
            UpdateAt = DateTime.UtcNow;
        }

        public bool ApplyCommand(string command)
        {
            if (!IsOnline || Status != DeviceStatus.Active)
                return false;

            switch (command.ToLower())
            {
                case "on": PowerState = true; break;
                case "off": PowerState = false; break;
                case "toggle": PowerState = !PowerState; break;
                default: return false;
            }
            UpdateAt = DateTime.UtcNow;
            return true;
        }

        public void MarkOnline() { IsOnline = true; UpdateAt = DateTime.UtcNow; }
        public void MarkOffline() { IsOnline = false; UpdateAt = DateTime.UtcNow; }
        public void MarkError() { Status = DeviceStatus.Error; UpdateAt = DateTime.UtcNow; }
        public void Decommission() { Status = DeviceStatus.Decommissioned; UpdateAt = DateTime.UtcNow; }
    }
}