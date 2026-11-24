using System;

namespace SmartHome.Domain.Entities
{
    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public bool IsSent { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
        public DateTime? SentAt { get; set; }

        public void MarkAsRead()
        {
            IsRead = true;
            ReadAt = DateTime.UtcNow;
        }
    }
}