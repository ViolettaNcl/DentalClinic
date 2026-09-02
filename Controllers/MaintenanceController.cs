using System.Security.Cryptography;
using System.Text;
using DentalClinic.Data;
using DentalClinic.Services;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> CleanupChatRetention(CancellationToken cancellationToken)
    {
        if (!HasValidCronSecret()) return Unauthorized();
        var service = new ChatRetentionService(_db, _configuration);
        var result = await service.CleanupAsync(cancellationToken);
        return Ok(result);
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
