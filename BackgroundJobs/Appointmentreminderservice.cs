using DentalClinic.Data;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.BackgroundJobs;

/// <summary>
/// Раз в заданный интервал проверяет подтверждённые записи, до которых остались
/// сутки (± интервал проверки), и отправляет пациенту уведомление-напоминание.
/// Использует IServiceScopeFactory, потому что DbContext — scoped-сервис,
/// а фоновая служба живёт как singleton всё время работы приложения.
/// </summary>
public class AppointmentReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentReminderService> _logger;
    private readonly IConfiguration _config;

    public AppointmentReminderService(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentReminderService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _config.GetValue<int?>("BackgroundJobs:ReminderCheckIntervalMinutes") ?? 60;
        var reminderHoursBefore = _config.GetValue<int?>("BackgroundJobs:ReminderHoursBefore") ?? 24;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        // Небольшая задержка перед первым запуском, чтобы приложение успело подняться
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (TaskCanceledException) { return; }

        do
        {
            try
            {
                await SendDueRemindersAsync(reminderHoursBefore, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в AppointmentReminderService");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendDueRemindersAsync(int reminderHoursBefore, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();

        // ВАЖНО: AppointmentDate во всём проекте хранится и сравнивается как
        // локальное время клиники, без часового пояса (см. DoctorScheduleController
        // и dateUtils.js на фронте — там та же логика "чисто локальное ISO").
        // Раньше здесь ошибочно использовался DateTime.UtcNow, из-за чего окно
        // напоминания было сдвинуто на разницу между UTC и локальным поясом
        // сервера — напоминания могли уходить на несколько часов раньше/позже
        // или вообще мимо окна проверки. DateTime.Now держит эту же логику
        // "локальное время" последовательно во всём проекте.
        var now = DateTime.Now;
        var windowStart = now.AddHours(reminderHoursBefore - 1);
        var windowEnd = now.AddHours(reminderHoursBefore + 1);

        // Подтверждённые записи зарегистрированных пациентов, до которых остались
        // примерно сутки, и напоминание по которым ещё не отправлялось
        var due = await db.AppointmentRequests
            .Where(r => r.PatientId != null
                     && r.Status == "confirmed"
                     && !r.ReminderSent
                     && r.AppointmentDate != null
                     && r.AppointmentDate >= windowStart
                     && r.AppointmentDate <= windowEnd)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        foreach (var request in due)
        {
            var dateText = request.AppointmentDate!.Value.ToString("dd.MM.yyyy HH:mm");
            await notifications.NotifyAsync(
                request.PatientId!.Value,
                "appointment_reminder",
                $"Напоминаем: завтра у вас приём в клинике ({dateText}) 🦷",
                request.Id);

            request.ReminderSent = true;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Отправлено напоминаний о приёме: {Count}", due.Count);
    }
}