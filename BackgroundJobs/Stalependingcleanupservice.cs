using DentalClinic.Services;

namespace DentalClinic.BackgroundJobs;

/// <summary>
/// Раз в сутки находит заявки в статусе "pending" старше N дней (админ так и не
/// подтвердил и не отклонил) и автоматически переводит их в "cancelled",
/// чтобы они не висели в очереди администратора бесконечно.
/// </summary>
public class StalePendingCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StalePendingCleanupService> _logger;
    private readonly IConfiguration _config;

    public StalePendingCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<StalePendingCleanupService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = _config.GetValue<int?>("BackgroundJobs:CleanupCheckIntervalHours") ?? 24;
        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (TaskCanceledException) { return; }

        do
        {
            try
            {
                var enabled = _config.GetValue<bool?>("BackgroundJobs:CleanupEnabled") ?? false;
                if (!enabled)
                {
                    _logger.LogInformation("StalePendingCleanupService: автоотмена выключена в настройках (BackgroundJobs:CleanupEnabled = false), пропускаю");
                }
                else
                {
                    using var scope = _scopeFactory.CreateScope();
                    var maintenance = scope.ServiceProvider.GetRequiredService<AppointmentMaintenanceService>();
                    await maintenance.CleanupStaleRequestsAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в StalePendingCleanupService");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

}
