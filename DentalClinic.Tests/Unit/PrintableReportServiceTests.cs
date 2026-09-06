using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class PrintableReportServiceTests
{
    [Fact]
    public void BuildReportHtml_UsesProvidedClinicTimestamp()
    {
        var generatedAt = new DateTime(2026, 1, 2, 3, 4, 0, DateTimeKind.Unspecified);
        var headers = new List<string> { "Пациент" };
        var rows = new List<IReadOnlyList<string>>
        {
            new List<string> { "<Иван>" }
        };

        var html = PrintableReportService.BuildReportHtml(
            "<Отчёт>",
            headers,
            rows,
            generatedAt);

        Assert.Contains("Сформировано: 02.01.2026 03:04", html);
        Assert.Contains("&lt;Отчёт&gt;", html);
        Assert.Contains("&lt;Иван&gt;", html);
    }
}
