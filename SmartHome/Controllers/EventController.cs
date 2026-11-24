using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHome.Data;

namespace SmartHome.Controllers;

[Authorize(Roles = "Owner")]
public class EventController(ApplicationDbContext context) : Controller
{
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
    {
        int ownerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var events = context.EventLogs.AsNoTracking().Where(e => e.UserId == ownerId ||
            (e.Source == "DevicesController" && e.EventType == Domain.Enums.EventType.Info &&
             context.Devices.Any(d => d.Id == e.DeviceId && d.OwnerId == ownerId)));

        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        if (!ModelState.IsValid || (from.HasValue && to.HasValue && from.Value > to.Value))
        {
            ModelState.AddModelError("", "Проверьте диапазон дат.");
            return View(Array.Empty<Domain.Entities.EventLog>());
        }

        if (from.HasValue)
        {
            var start = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            events = events.Where(e => e.Timestamp >= start);
        }
        if (to.HasValue)
        {
            var end = to.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            events = events.Where(e => e.Timestamp <= end);
        }
        return View(await events.OrderByDescending(e => e.Timestamp).ThenByDescending(e => e.Id).ToListAsync());
    }
}
