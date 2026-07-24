using DentalClinic.Data;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;

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
        var expiryDays = _config.GetValue<int?>("BackgroundJobs:PendingRequestExpiryDays") ?? 3;

        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (TaskCanceledException) { return; }

        do
        {
            try
            {
                var enabled = _config.GetValue<bool?>("BackgroundJobs:CleanupEnabled") ?? true;
                if (!enabled)
                {
                    _logger.LogInformation("StalePendingCleanupService: автоотмена выключена в настройках (BackgroundJobs:CleanupEnabled = false), пропускаю");
                }
                else
                {
                    await CleanupStaleRequestsAsync(expiryDays, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в StalePendingCleanupService");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupStaleRequestsAsync(int expiryDays, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var threshold = DateTime.UtcNow.AddDays(-expiryDays);

        var stale = await db.AppointmentRequests
            .Where(r => r.Status == "pending" && r.CreatedAt < threshold)
            .ToListAsync(ct);

        if (stale.Count == 0) return;

        foreach (var request in stale)
        {
            request.Status = "cancelled";
            request.Comment = string.IsNullOrWhiteSpace(request.Comment)
                ? "[Автоматически отменена: не обработана администратором]"
                : request.Comment + " [Автоматически отменена: не обработана администратором]";

            if (request.PatientId.HasValue)
            {
                await notifications.NotifyAsync(
                    request.PatientId.Value,
                    "appointment_cancelled",
                    "Ваша заявка на приём была автоматически отменена — она долго ждала подтверждения. Пожалуйста, запишитесь ещё раз или позвоните нам.",
                    request.Id);
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Автоматически отменено зависших заявок: {Count}", stale.Count);
    }
}