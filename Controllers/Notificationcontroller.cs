using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Data;
using System.Security.Claims;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Patient")]
public class NotificationController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public NotificationController(ApplicationDbContext context) => _context = context;

    // Последние уведомления пациента (для колокольчика)
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var patientId = GetCurrentUserId();

        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.PatientId == patientId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(30)
            .ToListAsync(cancellationToken);

        return Ok(notifications);
    }

    // Количество непрочитанных — для бейджа на колокольчике
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var patientId = GetCurrentUserId();
        var count = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.PatientId == patientId && !n.IsRead, cancellationToken);

        return Ok(new { count });
    }

    // Отметить одно уведомление прочитанным
    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var patientId = GetCurrentUserId();
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.PatientId == patientId, cancellationToken);

        if (notification == null) return NotFound();

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { notification.Id, notification.IsRead });
    }

    // Отметить все уведомления прочитанными
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var patientId = GetCurrentUserId();

        await _context.Notifications
            .Where(n => n.PatientId == patientId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), cancellationToken);

        return Ok(new { message = "✅ Все уведомления отмечены прочитанными" });
    }

    // Удалить одно уведомление (кнопка "корзина" в списке, с подтверждением на фронте)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var patientId = GetCurrentUserId();
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.PatientId == patientId, cancellationToken);

        if (notification == null) return NotFound();

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "🗑️ Уведомление удалено" });
    }

    // Удалить все уведомления пациента ("очистить всё", тоже с подтверждением на фронте)
    [HttpDelete]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken)
    {
        var patientId = GetCurrentUserId();

        await _context.Notifications
            .Where(n => n.PatientId == patientId)
            .ExecuteDeleteAsync(cancellationToken);

        return Ok(new { message = "🗑️ Все уведомления удалены" });
    }

    private int GetCurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}