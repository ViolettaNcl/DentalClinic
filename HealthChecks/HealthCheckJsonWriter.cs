using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DentalClinic.HealthChecks;

/// <summary>
/// Отдаёт /health не голым текстом ("Healthy"), а компактным JSON —
/// удобно для скриптов мониторинга и для UptimeRobot/Grafana и т.п.,
/// плюс сразу видно, какая именно проверка не прошла (например "db").
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
                durationMs = e.Value.Duration.TotalMilliseconds,
                error = e.Value.Exception?.Message
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
