using System.Security.Cryptography;
using System.Text;
using DentalClinic.Services;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/maintenance")]
public sealed class MaintenanceController : ControllerBase
{
    private readonly AppointmentMaintenanceService _maintenance;
    private readonly IConfiguration _configuration;

    public MaintenanceController(
        AppointmentMaintenanceService maintenance,
        IConfiguration configuration)
    {
        _maintenance = maintenance;
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
