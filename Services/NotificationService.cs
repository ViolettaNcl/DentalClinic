using DentalClinic.Data;
using DentalClinic.Hubs;
using DentalClinic.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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
        var notification = CreateNotification(patientId, type, message, relatedId, idempotencyKey: null);

        // Persistence is the durable source of truth for patient notifications.
        // Realtime delivery is only an optimization for an already-open browser tab.
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        await DeliverPatientRealtimeBestEffortAsync(notification);
    }

    /// <summary>
    /// Persist an identified notification at most once across multiple app instances.
    /// The preliminary lookup keeps routine repeated maintenance runs cheap; the
    /// database unique filtered index is the actual cross-instance race guarantee.
    /// Returns true only when this call created the durable notification.
    /// </summary>
    public async Task<bool> NotifyOnceAsync(
        int patientId,
        string type,
        string message,
        int? relatedId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        idempotencyKey = idempotencyKey?.Trim() ?? string.Empty;
        if (idempotencyKey.Length == 0 || idempotencyKey.Length > 120)
            throw new ArgumentOutOfRangeException(nameof(idempotencyKey), "Idempotency key must contain 1–120 characters.");

        if (await _db.Notifications
            .AsNoTracking()
            .AnyAsync(n => n.IdempotencyKey == idempotencyKey, cancellationToken))
        {
            return false;
        }

        var notification = CreateNotification(patientId, type, message, relatedId, idempotencyKey);
        _db.Notifications.Add(notification);

        try
        {
            // SaveChanges may also persist tracked maintenance state such as
            // AppointmentRequest.ReminderSent, keeping the winning path consistent.
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another instance may have inserted the same key after our initial
            // lookup. Detach only our losing notification, confirm that exact key
            // now exists, then persist any other tracked state (e.g. ReminderSent).
            _db.Entry(notification).State = EntityState.Detached;

            var wonElsewhere = await _db.Notifications
                .AsNoTracking()
                .AnyAsync(n => n.IdempotencyKey == idempotencyKey, cancellationToken);

            if (!wonElsewhere)
                throw;

            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Skipped duplicate durable notification with idempotency key {IdempotencyKey}",
                idempotencyKey);
            return false;
        }

        await DeliverPatientRealtimeBestEffortAsync(notification, cancellationToken);
        return true;
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

    private static Notification CreateNotification(
        int patientId,
        string type,
        string message,
        int? relatedId,
        string? idempotencyKey)
    {
        if (message.Length > 550)
            message = message[..547] + "...";

        return new Notification
        {
            PatientId = patientId,
            Type = type,
            Message = message,
            RelatedId = relatedId,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task DeliverPatientRealtimeBestEffortAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.Group($"patient-{notification.PatientId}").SendAsync(
                "ReceiveNotification",
                new
                {
                    notification.Id,
                    notification.Type,
                    notification.Message,
                    notification.RelatedId,
                    notification.IsRead,
                    notification.CreatedAt
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Realtime delivery failed for persisted notification {NotificationId} to patient {PatientId}",
                notification.Id,
                notification.PatientId);
        }
    }
}
