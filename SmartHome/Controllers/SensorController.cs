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
    [Authorize(Roles = "Owner")]
    public class SensorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SensorController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        // GET: Sensor
        public async Task<IActionResult> Index()
        {
            int ownerId = GetCurrentUserId();

            // Загружаем датчики, которые принадлежат комнатам этого владельца (или без комнаты)
            var sensors = await _context.Sensors
                .Include(s => s.Room)
                .Include(s => s.Readings.OrderByDescending(r => r.Timestamp).Take(5))
                .Where(s => (s.Room != null && s.Room.OwnerId == ownerId) || s.RoomId == null)
                .ToListAsync();

            return View(sensors);
        }

        // GET: Sensor/Create
        public async Task<IActionResult> Create()
        {
            int ownerId = GetCurrentUserId();

            ViewBag.Rooms = new SelectList(
                await _context.Rooms.Where(r => r.OwnerId == ownerId).ToListAsync(),
                "Id", "Name");

            return View();
        }

        // POST: Sensor/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Type,Unit,RoomId")] Sensor sensor)
        {
            int ownerId = GetCurrentUserId();

            if (ModelState.IsValid)
            {
                sensor.InstalledAt = DateTime.UtcNow;
                sensor.IsActive = true;

                _context.Add(sensor);
                await _context.SaveChangesAsync();

                _context.EventLogs.Add(new EventLog
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = EventType.Info,
                    Source = "SensorController",
                    Description = $"Добавлен датчик: {sensor.Name}",
                    UserId = ownerId,
                    SensorId = sensor.Id
                });
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Rooms = new SelectList(
                await _context.Rooms.Where(r => r.OwnerId == ownerId).ToListAsync(),
                "Id", "Name", sensor.RoomId);

            return View(sensor);
        }

        // POST: Sensor/ToggleActive/5
        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id)
        {
            int ownerId = GetCurrentUserId();
            var sensor = await _context.Sensors
                .Include(s => s.Room)
                .FirstOrDefaultAsync(s => s.Id == id && (s.Room.OwnerId == ownerId || s.RoomId == null));

            if (sensor != null)
            {
                if (sensor.IsActive)
                    sensor.Deactivate();
                else
                    sensor.Activate();

                await _context.SaveChangesAsync();

                _context.EventLogs.Add(new EventLog
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = EventType.Info,
                    Source = "SensorController",
                    Description = $"Датчик {sensor.Name} {(sensor.IsActive ? "активирован" : "деактивирован")}",
                    UserId = ownerId,
                    SensorId = sensor.Id
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Sensor/SimulateReading
        [HttpPost]
        public async Task<IActionResult> SimulateReading(int sensorId, double value)
        {
            int ownerId = GetCurrentUserId();
            var sensor = await _context.Sensors
                .Include(s => s.Room)
                .FirstOrDefaultAsync(s => s.Id == sensorId && (s.Room.OwnerId == ownerId || s.RoomId == null));

            if (sensor == null) return NotFound();

            if (!sensor.IsActive)
            {
                TempData["Error"] = "Датчик деактивирован, показания не принимаются.";
                return RedirectToAction(nameof(Index));
            }

            // 1. Создаем новое показание
            var reading = new SensorReading
            {
                SensorId = sensor.Id,
                Value = value,
                Unit = sensor.Unit,
                Timestamp = DateTime.UtcNow
            };
            _context.SensorReadings.Add(reading);

            // 2. Логируем показание
            _context.EventLogs.Add(new EventLog
            {
                Timestamp = DateTime.UtcNow,
                EventType = EventType.Info,
                Source = "SensorController",
                Description = $"Датчик '{sensor.Name}' зафиксировал значение: {value} {sensor.Unit}",
                UserId = ownerId,
                SensorId = sensor.Id
            });
            await _context.SaveChangesAsync();

            // 3. ПРОВЕРКА СЦЕНАРИЕВ (Scenario Engine)
            var activeScenarios = await _context.Scenarios
                .Include(s => s.Triggers)
                .Include(s => s.Actions)
                .Where(s => s.Status == ScenarioStatus.Active && s.AuthorId == ownerId)
                .ToListAsync();

            foreach (var scenario in activeScenarios)
            {
                // Проверяем, есть ли триггер на этот датчик
                var trigger = scenario.Triggers.FirstOrDefault(t => t.SensorId == sensor.Id);
                if (trigger != null && trigger.CheckCondition(value))
                {
                    // Условие выполнено! Выполняем действия
                    foreach (var action in scenario.Actions)
                    {
                        var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == action.DeviceId);
                        if (device != null && device.Status == DeviceStatus.Active)
                        {
                            // Выполняем команду над устройством
                            if (device.ApplyCommand(action.Type))
                            {
                                device.UpdateAt = DateTime.UtcNow;

                                // Логируем срабатывание сценария
                                _context.EventLogs.Add(new EventLog
                                {
                                    Timestamp = DateTime.UtcNow,
                                    EventType = EventType.Alert,
                                    Source = "ScenarioEngine",
                                    Description = $"Сработал сценарий '{scenario.Name}': устройство '{device.Name}' переведено в {action.Type}",
                                    UserId = ownerId,
                                    DeviceId = device.Id,
                                    ScenarioId = scenario.Id
                                });

                                // Создаем уведомление владельцу
                                _context.Notifications.Add(new Notification
                                {
                                    UserId = ownerId,
                                    Subject = "Сработал сценарий",
                                    Text = $"Сценарий '{scenario.Name}' выполнен. Устройство '{device.Name}' -> {action.Type}.",
                                    IsRead = false,
                                    IsSent = true,
                                    CreatedAt = DateTime.UtcNow,
                                    SentAt = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Показание {value} {sensor.Unit} записано.";
            return RedirectToAction(nameof(Index));
        }
    }
}