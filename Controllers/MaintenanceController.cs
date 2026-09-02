using System.Security.Cryptography;
using System.Text;
using DentalClinic.Data;
using DentalClinic.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/maintenance")]
public sealed class MaintenanceController : ControllerBase
{
    private readonly AppointmentMaintenanceService _maintenance;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public MaintenanceController(
        AppointmentMaintenanceService maintenance,
        ApplicationDbContext db,
        IConfiguration configuration)
    {
        _maintenance = maintenance;
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("reminders")]
    public async Task<IActionResult> SendReminders(CancellationToken cancellationToken)
    {
        if (!HasValidCronSecret()) return Unauthorized();
        var processed = await _maintenance.SendTomorrowRemindersAsync(cancellationToken);
        return Ok(new { processed });
    }

    [HttpGet("cleanup")]
    public async Task<IActionResult> Cleanup(CancellationToken cancellationToken)
    {
        if (!HasValidCronSecret()) return Unauthorized();
        var processed = await _maintenance.CleanupStaleRequestsAsync(cancellationToken);
        return Ok(new { processed });
    }

    [HttpGet("chat-retention")]
    public async Task<IActionResult> ChatRetention(CancellationToken cancellationToken)
    {
        if (!HasValidCronSecret()) return Unauthorized();

        var messageDays = Math.Clamp(_configuration.GetValue<int?>("ChatRetention:MessageDays") ?? 30, 1, 365);
        var ipDays = Math.Clamp(_configuration.GetValue<int?>("ChatRetention:IpDays") ?? 1, 0, messageDays);
        var now = DateTime.UtcNow;
        var messageCutoff = now.AddDays(-messageDays);
        var ipCutoff = now.AddDays(-ipDays);

        var oldMessages = await _db.ChatMessageLogs
            .Where(x => x.CreatedAt < messageCutoff)
            .ToListAsync(cancellationToken);
        var oldMessageIds = oldMessages.Select(x => x.Id).ToHashSet();

        var rowsWithOldIp = await _db.ChatMessageLogs
            .Where(x => x.ClientIp != null && x.CreatedAt < ipCutoff)
            .ToListAsync(cancellationToken);

        var clearedIps = 0;
        foreach (var row in rowsWithOldIp)
        {
            if (oldMessageIds.Contains(row.Id)) continue;
            row.ClientIp = null;
            clearedIps++;
        }

        _db.ChatMessageLogs.RemoveRange(oldMessages);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            deletedMessages = oldMessages.Count,
            clearedIps,
            messageRetentionDays = messageDays,
            ipRetentionDays = ipDays
        });
    }

    private bool HasValidCronSecret()
    {
        var expected = _configuration["CRON_SECRET"];
        var authorization = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(expected)
            || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        var supplied = authorization["Bearer ".Length..].Trim();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(supplied));
    }
}
