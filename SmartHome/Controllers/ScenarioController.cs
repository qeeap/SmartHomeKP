using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartHome.Data;
using SmartHome.Domain.Entities;
using SmartHome.Domain.Enums;
using SmartAction = SmartHome.Domain.Entities.Action; // Разрешение конфликта имен

namespace SmartHome.Controllers
{
    [Authorize(Roles = "Owner")]
    public class ScenarioController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ScenarioController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        // GET: Scenario
        public async Task<IActionResult> Index()
        {
            int ownerId = GetCurrentUserId();

            var scenarios = await _context.Scenarios
                .Include(s => s.Triggers)
                .Include(s => s.Actions)
                .Where(s => s.AuthorId == ownerId)
                .ToListAsync();

            return View(scenarios);
        }

        // GET: Scenario/Create
        public async Task<IActionResult> Create()
        {
            int ownerId = GetCurrentUserId();

            ViewBag.Sensors = new SelectList(
                await _context.Sensors.Where(s => s.OwnerId == ownerId && s.IsActive).ToListAsync(),
                "Id", "Name");

            ViewBag.Devices = new SelectList(
                await _context.Devices.Where(d => d.OwnerId == ownerId && d.Status == DeviceStatus.Active).ToListAsync(),
                "Id", "Name");

            return View();
        }

        // POST: Scenario/Create
        [HttpPost]
        public async Task<IActionResult> Create(string name, string? description, int sensorId, string op, double? threshold, int deviceId, string commandType)
        {
            int ownerId = GetCurrentUserId();

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(name) || threshold == null || !double.IsFinite(threshold.Value)
                || op is not (">" or "<" or ">=" or "<=" or "==") || commandType is not ("On" or "Off" or "Toggle")
                || !await _context.Sensors.AnyAsync(s => s.Id == sensorId && s.OwnerId == ownerId && s.IsActive)
                || !await _context.Devices.AnyAsync(d => d.Id == deviceId && d.OwnerId == ownerId && d.Status == DeviceStatus.Active))
            {
                ModelState.AddModelError("", "Проверьте датчик, устройство, условие и порог.");
                return await ReloadCreateView(ownerId);
            }

            // Создаем сценарий
            var scenario = new Scenario
            {
                AuthorId = ownerId,
                Name = name,
                Description = description,
                Status = ScenarioStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.Scenarios.Add(scenario);
            await _context.SaveChangesAsync();

            // Создаем триггер
            var trigger = new Trigger
            {
                ScenarioId = scenario.Id,
                SensorId = sensorId,
                Operator = op,
                Threshold = threshold.Value,
                Type = "Sensor"
            };
            _context.Triggers.Add(trigger);

            // Создаем действие
            var action = new SmartAction
            {
                ScenarioId = scenario.Id,
                DeviceId = deviceId,
                Type = commandType,
                Order = 1
            };
            _context.Actions.Add(action);

            await _context.SaveChangesAsync();

            // Запись в журнал
            _context.EventLogs.Add(new EventLog
            {
                Timestamp = DateTime.UtcNow,
                EventType = EventType.Info,
                Source = "ScenarioController",
                Description = $"Создан сценарий: {scenario.Name}",
                UserId = ownerId,
                ScenarioId = scenario.Id
            });
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: Scenario/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            int ownerId = GetCurrentUserId();
            var scenario = await _context.Scenarios.FirstOrDefaultAsync(s => s.Id == id && s.AuthorId == ownerId);

            if (scenario != null)
            {
                if (scenario.Status == ScenarioStatus.Active)
                    scenario.Deactivate();
                else
                    scenario.Activate();

                await _context.SaveChangesAsync();

                _context.EventLogs.Add(new EventLog
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = EventType.Info,
                    Source = "ScenarioController",
                    Description = $"Сценарий '{scenario.Name}' {(scenario.Status == ScenarioStatus.Active ? "активирован" : "деактивирован")}",
                    UserId = ownerId,
                    ScenarioId = scenario.Id
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> ReloadCreateView(int ownerId)
        {
            ViewBag.Sensors = new SelectList(
                await _context.Sensors.Where(s => s.OwnerId == ownerId && s.IsActive).ToListAsync(),
                "Id", "Name");
            ViewBag.Devices = new SelectList(
                await _context.Devices.Where(d => d.OwnerId == ownerId && d.Status == DeviceStatus.Active).ToListAsync(),
                "Id", "Name");
            return View();
        }
    }
}