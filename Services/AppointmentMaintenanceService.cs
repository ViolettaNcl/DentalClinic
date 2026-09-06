using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services;

/// <summary>
/// Задачи обслуживания записей. Их вызывает либо обычный BackgroundService,
/// либо cron/maintenance endpoint там, где постоянно работающий фоновый поток
/// не гарантируется.
/// </summary>
public sealed class AppointmentMaintenanceService
{
    private readonly ApplicationDbContext _db;
    private readonly NotificationService _notifications;
    private readonly ClinicClock _clock;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppointmentMaintenanceService> _logger;

    public AppointmentMaintenanceService(
        ApplicationDbContext db,
        NotificationService notifications,
        ClinicClock clock,
        IConfiguration configuration,
        ILogger<AppointmentMaintenanceService> logger)
    {
        _db = db;
        _notifications = notifications;
        _clock = clock;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<int> SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        var reminderHoursBefore = Math.Max(
            1,
            _configuration.GetValue<int?>("BackgroundJobs:ReminderHoursBefore") ?? 24);
        var now = _clock.Now;

        return SendRemindersInWindowAsync(
            now.AddHours(reminderHoursBefore - 1),
            now.AddHours(reminderHoursBefore + 1),
            cancellationToken);
    }

    /// <summary>
    /// Daily maintenance path: selects all appointments on the next clinic-local
    /// calendar day. The reminder itself includes the exact date/time rather than
    /// saying "tomorrow", so the same copy is correct for hourly and daily runs.
    /// </summary>
    public Task<int> SendTomorrowRemindersAsync(CancellationToken cancellationToken)
    {
        var tomorrow = _clock.Now.Date.AddDays(1);
        return SendRemindersInWindowAsync(tomorrow, tomorrow.AddDays(1), cancellationToken);
    }

    /// <summary>
    /// Sends one post-visit review prompt to registered patients after a completed
    /// appointment. FollowUpSent is the durable appointment-level delivery marker:
    /// deleting the user-facing notification must not make the maintenance job send
    /// the same prompt again. The legacy notification lookup remains for rows created
    /// before this marker existed, while NotifyOnceAsync + the unique database index
    /// protects cross-instance races for new maintenance runs.
    /// </summary>
    public async Task<int> SendPostVisitFollowUpsAsync(CancellationToken cancellationToken)
    {
        var delayHours = Math.Clamp(
            _configuration.GetValue<int?>("BackgroundJobs:FollowUpDelayHours") ?? 6,
            1,
            168);
        var lookbackDays = Math.Clamp(
            _configuration.GetValue<int?>("BackgroundJobs:FollowUpLookbackDays") ?? 7,
            1,
            30);

        var now = _clock.Now;
        var windowStart = now.AddDays(-lookbackDays);
        var windowEnd = now.AddHours(-delayHours);
        if (windowEnd <= windowStart) return 0;

        var due = await _db.AppointmentRequests
            .Where(AppointmentFollowUpPolicy.DueBetween(windowStart, windowEnd))
            .Where(request => !request.FollowUpSent)
            .Where(request => !_db.Notifications.Any(notification =>
                notification.PatientId == request.PatientId
                && notification.Type == AppointmentFollowUpPolicy.NotificationType
                && notification.RelatedId == request.Id))
            .OrderBy(request => request.AppointmentDate)
            .ThenBy(request => request.Id)
            .ToListAsync(cancellationToken);

        var createdCount = 0;
        foreach (var request in due)
        {
            // Mark before the durable notification save. NotifyOnceAsync uses this
            // same scoped DbContext, so both the winning insert and a duplicate-key
            // race persist the appointment marker. The marker survives later deletion
            // of the patient-facing Notification row.
            request.FollowUpSent = true;
            if (await _notifications.NotifyOnceAsync(
                request.PatientId!.Value,
                AppointmentFollowUpPolicy.NotificationType,
                AppointmentFollowUpPolicy.BuildMessage(request.AppointmentDate!.Value),
                request.Id,
                $"appointment-followup:{request.Id}",
                cancellationToken))
            {
                createdCount++;
            }
        }

        if (due.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Создано post-visit follow-up уведомлений: {Count}; кандидатов {Candidates}; окно {Start}–{End}",
            createdCount,
            due.Count,
            windowStart,
            windowEnd);
        return createdCount;
    }

    public async Task<int> CleanupStaleRequestsAsync(CancellationToken cancellationToken)
    {
        // Отмена заявок — потенциально разрушительная операция, поэтому без
        // явной настройки в production она выключена.
        var enabled = _configuration.GetValue<bool?>("BackgroundJobs:CleanupEnabled") ?? false;
        if (!enabled)
        {
            _logger.LogInformation("Автоматическая отмена необработанных заявок выключена");
            return 0;
        }

        var expiryDays = Math.Max(
            1,
            _configuration.GetValue<int?>("BackgroundJobs:PendingRequestExpiryDays") ?? 14);
        var threshold = DateTime.UtcNow.AddDays(-expiryDays);

        var stale = await _db.AppointmentRequests
            .Where(r => r.Status == AppointmentStatuses.Pending && r.CreatedAt < threshold)
            .ToListAsync(cancellationToken);

        foreach (var request in stale)
        {
            request.Status = AppointmentStatuses.Cancelled;
            request.Comment = string.IsNullOrWhiteSpace(request.Comment)
                ? "[Автоматически отменена: не обработана администратором]"
                : request.Comment + " [Автоматически отменена: не обработана администратором]";

            if (request.PatientId.HasValue)
            {
                // This path can also be invoked by a background worker and by a
                // maintenance endpoint at the same time. Use a durable key so the
                // cancellation itself may be replayed but the patient alert is not.
                await _notifications.NotifyOnceAsync(
                    request.PatientId.Value,
                    "appointment_cancelled",
                    "Ваша заявка на приём была автоматически отменена — она долго ждала подтверждения. Пожалуйста, запишитесь ещё раз или позвоните нам.",
                    request.Id,
                    $"appointment-auto-cancel:{request.Id}",
                    cancellationToken);
            }
        }

        if (stale.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Автоматически отменено необработанных заявок: {Count}", stale.Count);
        return stale.Count;
    }

    private async Task<int> SendRemindersInWindowAsync(
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken cancellationToken)
    {
        if (windowEnd <= windowStart)
            throw new ArgumentOutOfRangeException(nameof(windowEnd), "Reminder window end must be after its start.");

        var due = await _db.AppointmentRequests
            .Where(AppointmentReminderPolicy.DueBetween(windowStart, windowEnd))
            .OrderBy(r => r.AppointmentDate)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        var createdCount = 0;
        foreach (var request in due)
        {
            // Mark before the durable notification save. NotifyOnceAsync uses this
            // same scoped DbContext, so either the winning insert or the duplicate
            // path also persists ReminderSent. The unique key prevents two workers
            // that selected the row concurrently from creating two notifications.
            request.ReminderSent = true;
            if (await _notifications.NotifyOnceAsync(
                request.PatientId!.Value,
                "appointment_reminder",
                AppointmentReminderPolicy.BuildMessage(request.AppointmentDate!.Value),
                request.Id,
                $"appointment-reminder:{request.Id}",
                cancellationToken))
            {
                createdCount++;
            }
        }

        if (due.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Создано напоминаний о приёме: {Count}; кандидатов {Candidates}; окно {Start}–{End}",
            createdCount,
            due.Count,
            windowStart,
            windowEnd);
        return createdCount;
    }
}
