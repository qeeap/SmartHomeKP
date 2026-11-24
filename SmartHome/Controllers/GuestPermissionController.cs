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
    public class GuestPermissionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GuestPermissionController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        // GET: GuestPermission
        public async Task<IActionResult> Index()
        {
            int ownerId = GetCurrentUserId();

            var permissions = await _context.GuestPermissions
                .Where(p => p.OwnerId == ownerId)
                .ToListAsync();

            // Словарь имен гостей
            var guestIds = permissions.Select(p => p.GuestId).ToList();
            ViewBag.Guests = await _context.Users
                .Where(u => guestIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Login);

            // Словарь названий устройств (только этого владельца)
            var deviceIds = permissions.SelectMany(p => p.AllowedDeviceIds).Distinct().ToList();
            ViewBag.Devices = await _context.Devices
                .Where(d => deviceIds.Contains(d.Id) && d.OwnerId == ownerId)
                .ToDictionaryAsync(d => d.Id, d => d.Name);

            return View(permissions);
        }

        // GET: GuestPermission/Create
        public async Task<IActionResult> Create()
        {
            int ownerId = GetCurrentUserId();

            ViewBag.Guests = new SelectList(
                await _context.Users.Where(u => u.Role == UserRole.Guest).ToListAsync(),
                "Id", "Login");

            ViewBag.Devices = await _context.Devices
                .Where(d => d.OwnerId == ownerId && d.Status != DeviceStatus.Decommissioned)
                .ToListAsync();

            return View();
        }

        // POST: GuestPermission/Create
        [HttpPost]
        public async Task<IActionResult> Create(int guestId, DateTime expiresAt, List<int> selectedDeviceIds)
        {
            int ownerId = GetCurrentUserId();

            if (guestId == 0)
            {
                ModelState.AddModelError("", "Выберите гостя");
            }

            if (ModelState.IsValid)
            {
                var permission = new GuestPermission
                {
                    OwnerId = ownerId,
                    GuestId = guestId,
                    ExpiresAt = expiresAt,
                    Status = PermissionStatus.Active,
                    AllowedDeviceIds = selectedDeviceIds ?? new List<int>()
                };

                _context.GuestPermissions.Add(permission);
                await _context.SaveChangesAsync();

                _context.EventLogs.Add(new EventLog
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = EventType.Info,
                    Source = "GuestPermissionController",
                    Description = $"Выдан гостевой доступ пользователю ID={guestId}",
                    UserId = ownerId
                });
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            // Если ошибка, возвращаем списки обратно
            ViewBag.Guests = new SelectList(await _context.Users.Where(u => u.Role == UserRole.Guest).ToListAsync(), "Id", "Login", guestId);
            ViewBag.Devices = await _context.Devices.Where(d => d.OwnerId == ownerId && d.Status != DeviceStatus.Decommissioned).ToListAsync();

            return View();
        }

        // POST: GuestPermission/Suspend/5
        [HttpPost]
        public async Task<IActionResult> Suspend(int id) => await ChangeStatus(id, p => p.Suspend(), "Разрешение приостановлено");

        // POST: GuestPermission/Resume/5
        [HttpPost]
        public async Task<IActionResult> Resume(int id) => await ChangeStatus(id, p => p.Resume(), "Разрешение возобновлено");

        // POST: GuestPermission/Revoke/5
        [HttpPost]
        public async Task<IActionResult> Revoke(int id) => await ChangeStatus(id, p => p.Revoke(), "Разрешение отозвано");

        // POST: GuestPermission/Renew/5
        [HttpPost]
        public async Task<IActionResult> Renew(int id)
        {
            int ownerId = GetCurrentUserId();
            var permission = await _context.GuestPermissions.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId);

            if (permission != null)
            {
                permission.Renew(DateTime.UtcNow.AddDays(7));
                await _context.SaveChangesAsync();

                _context.EventLogs.Add(new EventLog
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = EventType.Info,
                    Source = "GuestPermissionController",
                    Description = $"Разрешение для гостя ID={permission.GuestId} продлено на 7 дней",
                    UserId = ownerId
                });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> ChangeStatus(int id, System.Action<GuestPermission> action, string logMessage)
        {
            int ownerId = GetCurrentUserId();
            var permission = await _context.GuestPermissions.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId);

            if (permission != null)
            {
                action(permission);
                await _context.SaveChangesAsync();

                _context.EventLogs.Add(new EventLog
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = EventType.Info,
                    Source = "GuestPermissionController",
                    Description = $"{logMessage} (Гость ID={permission.GuestId})",
                    UserId = ownerId
                });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}