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
                await _notifications.NotifyAsync(
                    request.PatientId.Value,
                    "appointment_cancelled",
                    "Ваша заявка на приём была автоматически отменена — она долго ждала подтверждения. Пожалуйста, запишитесь ещё раз или позвоните нам.",
                    request.Id);
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

        foreach (var request in due)
        {
            // NotificationService uses this same scoped DbContext. Marking the
            // appointment before NotifyAsync means the flag and notification are
            // persisted together by its SaveChanges call; a later run will skip it.
            request.ReminderSent = true;
            await _notifications.NotifyAsync(
                request.PatientId!.Value,
                "appointment_reminder",
                AppointmentReminderPolicy.BuildMessage(request.AppointmentDate!.Value),
                request.Id);
        }

        if (due.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Отправлено напоминаний о приёме: {Count}; окно {Start}–{End}",
            due.Count,
            windowStart,
            windowEnd);
        return due.Count;
    }
}
