using System.Text;
using DentalClinic.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class HealthCheckJsonWriterTests
{
    [Fact]
    public async Task PublicHealthPayload_DoesNotExposeProviderExceptionDetails()
    {
        const string secretInfrastructureDetail = "Server=private-sql.internal;Database=DentalClinicProd";
        var entry = new HealthReportEntry(
            HealthStatus.Unhealthy,
            description: "database unavailable",
            duration: TimeSpan.FromMilliseconds(12),
            exception: new InvalidOperationException(secretInfrastructureDetail),
            data: new Dictionary<string, object>());
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry> { ["db"] = entry },
            TimeSpan.FromMilliseconds(12));

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthCheckJsonWriter.WriteResponse(context, report);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();

        Assert.Contains("\"status\":\"Unhealthy\"", body);
        Assert.Contains("\"name\":\"db\"", body);
        Assert.DoesNotContain(secretInfrastructureDetail, body, StringComparison.Ordinal);
        Assert.DoesNotContain("private-sql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("error", body, StringComparison.OrdinalIgnoreCase);
    }
}
