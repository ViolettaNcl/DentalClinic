using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class AdminAnalyticsServiceTests
{
    [Fact]
    public async Task Summary_UsesExclusiveSources_Statuses_AndClinicMonth()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"analytics-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ApplicationDbContext(options);

        db.Doctors.AddRange(
            new Doctor { Id = 1, FullName = "Dr. One" },
            new Doctor { Id = 2, FullName = "Dr. Two" });

        db.AppointmentRequests.AddRange(
            Request(1, AppointmentStatuses.Pending, new DateTime(2026, 9, 6, 10, 0, 0), doctorId: 1, patientId: 10),
            Request(2, AppointmentStatuses.Confirmed, new DateTime(2026, 9, 5, 11, 0, 0), doctorId: 1),
            Request(3, AppointmentStatuses.Completed, new DateTime(2026, 9, 1, 12, 0, 0), doctorId: 2, patientId: 20, comment: "[Заявка через чат] Имплант"),
            Request(4, AppointmentStatuses.Cancelled, new DateTime(2026, 8, 31, 9, 0, 0)),
            Request(5, "legacy-status", null));
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Scheduling:TimeZoneId"] = "UTC" })
            .Build();
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new AdminAnalyticsService(db, new ClinicClock(config, time));

        var summary = await service.GetSummaryAsync();

        Assert.Equal(5, summary.TotalRequests);
        Assert.Equal(3, summary.ThisMonthRequests);
        Assert.Equal(40d, summary.ConfirmedOrCompletedRate);

        Assert.Equal(1, summary.Statuses.Pending);
        Assert.Equal(1, summary.Statuses.Confirmed);
        Assert.Equal(1, summary.Statuses.Completed);
        Assert.Equal(1, summary.Statuses.Cancelled);
        Assert.Equal(1, summary.Statuses.Unknown);

        Assert.Equal(1, summary.Sources.Registered);
        Assert.Equal(3, summary.Sources.Guest);
        Assert.Equal(1, summary.Sources.Denta);
        Assert.Equal(summary.TotalRequests,
            summary.Sources.Registered + summary.Sources.Guest + summary.Sources.Denta);

        Assert.Equal(30, summary.ByDay.Count);
        Assert.Equal("2026-08-08", summary.ByDay[0].Date);
        Assert.Equal("2026-09-06", summary.ByDay[^1].Date);
        Assert.Equal(1, summary.ByDay.Single(x => x.Date == "2026-09-01").Count);
        Assert.Equal(1, summary.ByDay.Single(x => x.Date == "2026-09-05").Count);
        Assert.Equal(1, summary.ByDay.Single(x => x.Date == "2026-09-06").Count);

        Assert.Collection(summary.ByDoctor,
            first =>
            {
                Assert.Equal(1, first.DoctorId);
                Assert.Equal("Dr. One", first.DoctorName);
                Assert.Equal(2, first.Count);
            },
            second =>
            {
                Assert.Equal(2, second.DoctorId);
                Assert.Equal("Dr. Two", second.DoctorName);
                Assert.Equal(1, second.Count);
            });
    }

    [Fact]
    public async Task Summary_EmptyDatabase_ReturnsStableZeroSeries()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"analytics-empty-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ApplicationDbContext(options);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Scheduling:TimeZoneId"] = "UTC" })
            .Build();
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new AdminAnalyticsService(db, new ClinicClock(config, time));

        var summary = await service.GetSummaryAsync();

        Assert.Equal(0, summary.TotalRequests);
        Assert.Equal(0d, summary.ConfirmedOrCompletedRate);
        Assert.Equal(30, summary.ByDay.Count);
        Assert.All(summary.ByDay, day => Assert.Equal(0, day.Count));
        Assert.Empty(summary.ByDoctor);
    }

    private static AppointmentRequest Request(
        int id,
        string status,
        DateTime? appointmentDate,
        int? doctorId = null,
        int? patientId = null,
        string? comment = null) => new()
    {
        Id = id,
        Phone = $"+70000000{id:000}",
        Status = status,
        AppointmentDate = appointmentDate,
        DoctorId = doctorId,
        PatientId = patientId,
        Comment = comment,
        CreatedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc)
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
