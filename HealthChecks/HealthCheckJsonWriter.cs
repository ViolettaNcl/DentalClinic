using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DentalClinic.HealthChecks;

/// <summary>
/// Public machine-readable health response. It intentionally exposes only the
/// check name/status/duration: provider exception messages can contain database
/// host names, connection details or other infrastructure metadata and must not
/// be returned by an unauthenticated endpoint.
/// </summary>
public static class HealthCheckJsonWriter
{
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = e.Value.Duration.TotalMilliseconds
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
