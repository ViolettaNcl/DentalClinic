using DentalClinic.Services;

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
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        // Небольшая задержка перед первым запуском, чтобы приложение успело подняться
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (TaskCanceledException) { return; }

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var maintenance = scope.ServiceProvider.GetRequiredService<AppointmentMaintenanceService>();
                await maintenance.SendDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в AppointmentReminderService");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

}
