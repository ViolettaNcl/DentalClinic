using DentalClinic.Data;
using DentalClinic.Hubs;
using DentalClinic.Models;
using Microsoft.AspNetCore.SignalR;

namespace DentalClinic.Services;

public class NotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationService(ApplicationDbContext db, IHubContext<NotificationHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task NotifyAsync(int patientId, string type, string message, int? relatedId = null)
    {
        if (message.Length > 550)
            message = message[..547] + "...";

        var notification = new Notification
        {
            PatientId = patientId,
            Type = type,
            Message = message,
            RelatedId = relatedId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        // Realtime: толкаем событие пациенту, если он сейчас онлайн (открыта вкладка сайта).
        // Если нет соединения — ничего страшного, колокольчик подтянет его при следующем
        // опросе/логине через обычный GET /api/notification.
        await _hub.Clients.Group($"patient-{patientId}").SendAsync("ReceiveNotification", new
        {
            notification.Id,
            notification.Type,
            notification.Message,
            notification.RelatedId,
            notification.IsRead,
            notification.CreatedAt
        });
    }

    /// <summary>
    /// Уведомление для всех подключённых администраторов (новая заявка, новый отзыв на
    /// модерацию и т.п.) — не сохраняется в БД, только живой пуш в открытые вкладки админки.
    /// </summary>
    public async Task NotifyAdminsAsync(string type, string message, int? relatedId = null)
    {
        await _hub.Clients.Group("admins").SendAsync("ReceiveAdminNotification", new
        {
            type,
            message,
            relatedId,
            createdAt = DateTime.UtcNow
        });
    }
}