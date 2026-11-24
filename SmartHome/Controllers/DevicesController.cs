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
    [Authorize]
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
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var role = User.FindFirstValue(ClaimTypes.Role);

            IQueryable<Device> devicesQuery = _context.Devices
                .Include(d => d.Room)
                .Where(d => d.Status != DeviceStatus.Decommissioned);

            if (role == "Owner")
            {
                devicesQuery = devicesQuery.Where(d => d.OwnerId == userId);
            }
            else if (role == "Guest")
            {
                var permissions = await _context.GuestPermissions
                    .Where(p => p.GuestId == userId && p.Status == PermissionStatus.Active && p.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                var allowedDeviceIds = permissions.SelectMany(p => p.AllowedDeviceIds).Distinct().ToList();
                devicesQuery = devicesQuery.Where(d => allowedDeviceIds.Contains(d.Id));
            }

            var devices = await devicesQuery.ToListAsync();
            return View(devices);
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
            var role = User.FindFirstValue(ClaimTypes.Role);

            var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == id);

            if (device == null) return NotFound();

            if (role == "Guest")
            {
                var permission = await _context.GuestPermissions
                    .FirstOrDefaultAsync(p => p.GuestId == userId && p.OwnerId == device.OwnerId);

                if (permission == null || !permission.Allows(device.Id))
                {
                    TempData["Error"] = "Доступ к устройству запрещен или истек";

                    _context.EventLogs.Add(new EventLog
                    {
                        Timestamp = DateTime.UtcNow,
                        EventType = EventType.Warning,
                        Source = "DevicesController",
                        Description = $"Попытка несанкционированного управления устройством {device.Name}",
                        UserId = userId,
                        DeviceId = device.Id
                    });
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }
            else if (role == "Owner" && device.OwnerId != userId)
            {
                return NotFound();
            }

            if (device.Status == DeviceStatus.Active)
            {
                bool success = device.ApplyCommand("Toggle");
                if (success)
                {
                    await _context.SaveChangesAsync();
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