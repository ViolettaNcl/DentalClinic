using DentalClinic.Controllers;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class AdminStatsExportControllerTests
{
    [Theory]
    [InlineData("not-a-date", "2026-09-06")]
    [InlineData("2026-09-01", "09/06/2026")]
    [InlineData("2026-9-1", "2026-09-06")]
    public async Task ExportReport_InvalidExplicitDate_ReturnsBadRequest(string from, string to)
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.ExportReport(from, to, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ExportXlsx_ReversedOrOversizedRange_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>(
            await controller.ExportXlsx("2026-09-06", "2026-09-01", CancellationToken.None));
        Assert.IsType<BadRequestObjectResult>(
            await controller.ExportXlsx("2025-09-05", "2026-09-06", CancellationToken.None));
    }

    [Fact]
    public async Task ExportReport_OmittedDates_UsesThirtyDayDefaultWindow()
    {
        await using var db = CreateDb();
        db.AppointmentRequests.AddRange(
            Request(1, new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc), "Inside"),
            Request(2, new DateTime(2026, 8, 6, 23, 59, 59, DateTimeKind.Utc), "Outside"));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.ExportReport(null, null, CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Contains("Inside", content.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Outside", content.Content, StringComparison.Ordinal);
        Assert.Contains("07.08.2026 – 06.09.2026", content.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportReport_ValidRange_FiltersByClinicCalendarDays()
    {
        await using var db = CreateDb();
        db.AppointmentRequests.AddRange(
            Request(1, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), "Start"),
            Request(2, new DateTime(2026, 9, 2, 23, 59, 59, DateTimeKind.Utc), "End"),
            Request(3, new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc), "Outside"));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.ExportReport("2026-09-01", "2026-09-02", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Contains("Start", content.Content, StringComparison.Ordinal);
        Assert.Contains("End", content.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Outside", content.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportReport_MoreThanConfiguredRowLimit_ReturnsUnprocessableEntityInsteadOfTruncating()
    {
        await using var db = CreateDb();
        db.AppointmentRequests.AddRange(
            Enumerable.Range(1, 101)
                .Select(i => Request(
                    i,
                    new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
                    $"Patient {i}")));
        await db.SaveChangesAsync();
        var controller = CreateController(db, maxRows: 100);

        var result = await controller.ExportReport("2026-09-01", "2026-09-01", CancellationToken.None);

        Assert.IsType<UnprocessableEntityObjectResult>(result);
    }

    [Fact]
    public async Task ExportReport_ExactlyConfiguredRowLimit_RemainsExportable()
    {
        await using var db = CreateDb();
        db.AppointmentRequests.AddRange(
            Enumerable.Range(1, 100)
                .Select(i => Request(
                    i,
                    new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
                    $"Patient {i}")));
        await db.SaveChangesAsync();
        var controller = CreateController(db, maxRows: 100);

        var result = await controller.ExportReport("2026-09-01", "2026-09-01", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Contains("Patient 100", content.Content, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"admin-export-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AdminStatsController CreateController(ApplicationDbContext db, int maxRows = 25_000)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduling:TimeZoneId"] = "UTC",
                ["AdminExports:MaxRows"] = maxRows.ToString()
            })
            .Build();
        var clock = new ClinicClock(
            config,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)));
        var analytics = new AdminAnalyticsService(db, clock);
        return new AdminStatsController(db, clock, analytics, config);
    }

    private static AppointmentRequest Request(int id, DateTime createdAt, string name) => new()
    {
        Id = id,
        FirstName = name,
        Phone = "+79990000000",
        Status = AppointmentStatuses.Pending,
        CreatedAt = createdAt
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
