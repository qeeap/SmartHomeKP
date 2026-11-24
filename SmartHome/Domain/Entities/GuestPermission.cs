using System;
using System.Collections.Generic;
using SmartHome.Domain.Enums;

namespace SmartHome.Domain.Entities
{
    public class GuestPermission
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public int GuestId { get; set; }
        public PermissionStatus Status { get; set; }
        public DateTime ExpiresAt { get; set; }
        public List<int> AllowedDeviceIds { get; set; } = new();

        public void Revoke() => Status = PermissionStatus.Revoked;
        public void Suspend() => Status = PermissionStatus.Suspended;
        public void Resume() => Status = PermissionStatus.Active;

        public void Renew(DateTime newDate)
        {
            if (newDate <= DateTime.UtcNow)
                throw new ArgumentException("New date must be in the future");
            ExpiresAt = newDate;
            Status = PermissionStatus.Active;
        }

        public bool Allows(int deviceId)
        {
            return Status == PermissionStatus.Active &&
                   DateTime.UtcNow <= ExpiresAt &&
                   AllowedDeviceIds.Contains(deviceId);
        }
    }
}