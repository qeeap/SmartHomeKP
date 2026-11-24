using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SmartHome.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Action = SmartHome.Domain.Entities.Action;

namespace SmartHome.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<GuestPermission> GuestPermissions { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<Sensor> Sensors { get; set; }
        public DbSet<SensorReading> SensorReadings { get; set; }
        public DbSet<Scenario> Scenarios { get; set; }
        public DbSet<Trigger> Triggers { get; set; }
        public DbSet<Action> Actions { get; set; }
        public DbSet<EventLog> EventLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Композиция (Каскадное удаление)
            modelBuilder.Entity<Scenario>()
                .HasMany(s => s.Triggers)
                .WithOne()
                .HasForeignKey(t => t.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Scenario>()
                .HasMany(s => s.Actions)
                .WithOne()
                .HasForeignKey(a => a.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Sensor>()
                .HasMany(s => s.Readings)
                .WithOne()
                .HasForeignKey(r => r.SensorId)
                .OnDelete(DeleteBehavior.Cascade);

            // 2. Агрегация (SetNull) 
            modelBuilder.Entity<Room>()
                .HasMany(r => r.Devices)
                .WithOne(d => d.Room) 
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Room>()
                .HasMany(r => r.Sensors)
                .WithOne(s => s.Room) 
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.SetNull);

            // 3. Настройка хранения списка ID через JSON
            var allowedDeviceIdsComparer = new ValueComparer<List<int>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? new List<int>() : c.ToList());

            modelBuilder.Entity<GuestPermission>()
                .Property(g => g.AllowedDeviceIds)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<List<int>>(v, (JsonSerializerOptions)null) ?? new List<int>()
                )
                .Metadata.SetValueComparer(allowedDeviceIdsComparer);

            // 4. Остальные связи (Restrict)
            modelBuilder.Entity<EventLog>().HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EventLog>().HasOne<Device>().WithMany().HasForeignKey(e => e.DeviceId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EventLog>().HasOne<Sensor>().WithMany().HasForeignKey(e => e.SensorId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EventLog>().HasOne<Scenario>().WithMany().HasForeignKey(e => e.ScenarioId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}