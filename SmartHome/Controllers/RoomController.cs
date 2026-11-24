using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHome.Data;
using SmartHome.Domain.Entities;
using SmartHome.Domain.Enums;

namespace SmartHome.Controllers
{
    [Authorize(Roles = "Owner")]
    public class RoomController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoomController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        // GET: Room
        public async Task<IActionResult> Index()
        {
            int ownerId = GetCurrentUserId();

            // Загружаем комнаты вместе с устройствами и датчиками для подсчета
            var rooms = await _context.Rooms
                .Include(r => r.Devices)
                .Include(r => r.Sensors)
                .Where(r => r.OwnerId == ownerId)
                .ToListAsync();

            return View(rooms);
        }

        // GET: Room/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Room/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Floor,Area")] Room room)
        {
            int ownerId = GetCurrentUserId();

            // Убираем OwnerId из валидации, так как мы задаем его вручную
            ModelState.Remove("OwnerId");

            if (ModelState.IsValid)
            {
                room.OwnerId = ownerId;
                _context.Add(room);
                await _context.SaveChangesAsync();

                // Запись в журнал
                _context.EventLogs.Add(new EventLog
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = EventType.Info,
                    Source = "RoomController",
                    Description = $"Создано помещение: {room.Name}",
                    UserId = ownerId
                });
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // GET: Room/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            int ownerId = GetCurrentUserId();
            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId);

            if (room == null) return NotFound();

            return View(room);
        }

        // POST: Room/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Floor,Area")] Room room)
        {
            if (id != room.Id) return NotFound();

            int ownerId = GetCurrentUserId();
            ModelState.Remove("OwnerId");

            if (ModelState.IsValid)
            {
                try
                {
                    // Находим комнату в БД, чтобы обновить её
                    var existingRoom = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId);
                    if (existingRoom == null) return NotFound();

                    // Вызываем метод из модели для смены имени
                    existingRoom.Rename(room.Name);
                    existingRoom.Floor = room.Floor;
                    existingRoom.Area = room.Area;

                    _context.Update(existingRoom);
                    await _context.SaveChangesAsync();

                    // Запись в журнал
                    _context.EventLogs.Add(new EventLog
                    {
                        Timestamp = DateTime.UtcNow,
                        EventType = EventType.Info,
                        Source = "RoomController",
                        Description = $"Изменено помещение: {existingRoom.Name}",
                        UserId = ownerId
                    });
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoomExists(room.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // GET: Room/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            int ownerId = GetCurrentUserId();
            var room = await _context.Rooms
                .FirstOrDefaultAsync(m => m.Id == id && m.OwnerId == ownerId);

            if (room == null) return NotFound();

            return View(room);
        }

        // POST: Room/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            int ownerId = GetCurrentUserId();
            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId);

            if (room != null)
            {
                _context.Rooms.Remove(room);
                await _context.SaveChangesAsync();

                // Запись в журнал
                _context.EventLogs.Add(new EventLog
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = EventType.Info,
                    Source = "RoomController",
                    Description = $"Удалено помещение: {room.Name}",
                    UserId = ownerId
                });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(e => e.Id == id);
        }
    }
}