using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services;

/// <summary>
/// Задачи обслуживания записей. Их вызывает либо обычный BackgroundService,
/// либо Vercel Cron, где постоянно работающий фоновый поток не гарантируется.
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
    /// Суточный cron Vercel запускается один раз утром и выбирает все записи
    /// следующего календарного дня в часовом поясе клиники.
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
        var due = await _db.AppointmentRequests
            .Where(r => r.PatientId != null
                     && r.Status == AppointmentStatuses.Confirmed
                     && !r.ReminderSent
                     && r.AppointmentDate != null
                     && r.AppointmentDate >= windowStart
                     && r.AppointmentDate < windowEnd)
            .ToListAsync(cancellationToken);

        foreach (var request in due)
        {
            var dateText = request.AppointmentDate!.Value.ToString("dd.MM.yyyy HH:mm");
            // NotificationService использует тот же scoped DbContext и сохраняет
            // уведомление вместе с флагом. Так повтор cron не создаст дубль.
            request.ReminderSent = true;
            await _notifications.NotifyAsync(
                request.PatientId!.Value,
                "appointment_reminder",
                $"Напоминаем: завтра у вас приём в клинике ({dateText}) 🦷",
                request.Id);
        }

        if (due.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Отправлено напоминаний о приёме: {Count}", due.Count);
        return due.Count;
    }
}
