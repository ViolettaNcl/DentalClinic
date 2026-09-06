using DentalClinic.Data;
using DentalClinic.Hubs;
using DentalClinic.Models;
using Microsoft.AspNetCore.SignalR;

namespace DentalClinic.Services;

public class NotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext db,
        IHubContext<NotificationHub> hub,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
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

        // Persistence is the durable source of truth for patient notifications.
        // Realtime delivery is only an optimization for an already-open browser tab.
        // Do not let a transient SignalR failure turn a successfully committed
        // appointment/review operation into an HTTP 500 that the user may retry.
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Realtime delivery failed for persisted notification {NotificationId} to patient {PatientId}",
                notification.Id,
                patientId);
        }
    }

    /// <summary>
    /// Уведомление для всех подключённых администраторов (новая заявка, новый отзыв на
    /// модерацию и т.п.) — не сохраняется в БД, только живой пуш в открытые вкладки админки.
    /// Это побочный realtime-канал: его отказ не должен отменять уже сохранённую заявку/отзыв.
    /// </summary>
    public async Task NotifyAdminsAsync(string type, string message, int? relatedId = null)
    {
        try
        {
            await _hub.Clients.Group("admins").SendAsync("ReceiveAdminNotification", new
            {
                type,
                message,
                relatedId,
                createdAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Realtime admin notification delivery failed for type {Type} and related id {RelatedId}",
                type,
                relatedId);
        }
    }
}