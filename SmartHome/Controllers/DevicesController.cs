using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartHome.Data;
using SmartHome.Domain.Entities;
using SmartHome.Domain.Enums;

namespace SmartHome.Controllers
{
    [Authorize(Roles = "Owner,Guest")]
    public class DevicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DevicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Devices
        public async Task<IActionResult> Index()
        {
            return View(await AccessibleDevices().Include(d => d.Room).ToListAsync());
        }

        private IQueryable<Device> AccessibleDevices()
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var devices = _context.Devices.Where(d => d.Status != DeviceStatus.Decommissioned);
            if (User.IsInRole("Owner"))
                return devices.Where(d => d.OwnerId == userId);

            // В каждом гостевом разрешении номера доступных устройств хранятся в массиве JSON.
            return _context.Devices.FromSqlInterpolated($"""
                SELECT d.* FROM Devices AS d
                WHERE d.Status <> {(int)DeviceStatus.Decommissioned}
                  AND EXISTS (
                      SELECT 1 FROM GuestPermissions AS p
                      CROSS APPLY OPENJSON(p.AllowedDeviceIds) WITH (DeviceId int '$') AS allowed
                      WHERE p.OwnerId = d.OwnerId AND p.GuestId = {userId}
                        AND p.Status = {(int)PermissionStatus.Active}
                        AND p.ExpiresAt > SYSUTCDATETIME() AND allowed.DeviceId = d.Id
                  )
                """);
        }

        // GET: Devices/Create
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Create()
        {
            int ownerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            ViewBag.Rooms = new SelectList(
                await _context.Rooms.Where(r => r.OwnerId == ownerId).ToListAsync(),
                "Id", "Name");

            return View();
        }

        // POST: Devices/Create
        [HttpPost]
        [Authorize(Roles = "Owner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Type,Model,RoomId")] Device device)
        {
            int ownerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            ModelState.Remove("OwnerId");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdateAt");

            if (device.RoomId.HasValue && !await _context.Rooms.AnyAsync(r => r.Id == device.RoomId && r.OwnerId == ownerId))
                ModelState.AddModelError("RoomId", "Выберите помещение своего дома.");

            if (ModelState.IsValid)
            {
                device.OwnerId = ownerId;
                device.CreatedAt = DateTime.UtcNow;
                device.UpdateAt = DateTime.UtcNow;
                device.Status = DeviceStatus.Active;
                device.IsOnline = true;
                device.PowerState = false;

                _context.Add(device);
                await _context.SaveChangesAsync();

                _context.EventLogs.Add(new EventLog
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = EventType.Info,
                    Source = "DevicesController",
                    Description = $"Добавлено новое устройство: {device.Name}",
                    UserId = ownerId,
                    DeviceId = device.Id
                });
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Rooms = new SelectList(
                await _context.Rooms.Where(r => r.OwnerId == ownerId).ToListAsync(),
                "Id", "Name", device.RoomId);

            return View(device);
        }

        // POST: Devices/TogglePower/5
        [HttpPost]
        public async Task<IActionResult> TogglePower(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var device = await AccessibleDevices().FirstOrDefaultAsync(d => d.Id == id);
            if (device == null) return NotFound();

            if (device.Status == DeviceStatus.Active)
            {
                bool success = device.ApplyCommand("Toggle");
                if (success)
                {
                    _context.EventLogs.Add(new EventLog
                    {
                        Timestamp = DateTime.UtcNow,
                        EventType = EventType.Info,
                        Source = "DevicesController",
                        Description = $"Устройство {device.Name} переключено на {(device.PowerState ? "Вкл" : "Выкл")}",
                        UserId = userId,
                        DeviceId = device.Id
                    });
                    await _context.SaveChangesAsync();
                }
            }

            await transaction.CommitAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: Devices/Decommission/5
        [HttpPost]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Decommission(int id)
        {
            int ownerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == ownerId);

            if (device != null)
            {
                device.Decommission();
                await _context.SaveChangesAsync();

                _context.EventLogs.Add(new EventLog
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = EventType.Warning,
                    Source = "DevicesController",
                    Description = $"Устройство {device.Name} снято с учета",
                    UserId = ownerId,
                    DeviceId = device.Id
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}