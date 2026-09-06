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
    public async Task Summary_UsesExclusiveSources_Statuses_AndRequestCreationDates()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"analytics-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ApplicationDbContext(options);

        db.Doctors.AddRange(
            new Doctor { Id = 1, FullName = "Dr. One" },
            new Doctor { Id = 2, FullName = "Dr. Two" });

        db.AppointmentRequests.AddRange(
            Request(1, " PENDING ", new DateTime(2026, 9, 6, 10, 0, 0), doctorId: 1, patientId: 10,
                createdAt: new DateTime(2026, 9, 6, 7, 0, 0, DateTimeKind.Utc)),
            // The appointment itself is next month, but the request was created this month.
            Request(2, "CONFIRMED", new DateTime(2026, 10, 5, 11, 0, 0), doctorId: 1,
                createdAt: new DateTime(2026, 9, 5, 8, 0, 0, DateTimeKind.Utc)),
            Request(3, AppointmentStatuses.Completed, new DateTime(2026, 9, 1, 12, 0, 0), doctorId: 2, patientId: 20,
                comment: "[Заявка через чат] Имплант",
                createdAt: new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc)),
            Request(4, AppointmentStatuses.Cancelled, new DateTime(2026, 8, 31, 9, 0, 0),
                createdAt: new DateTime(2026, 8, 31, 10, 0, 0, DateTimeKind.Utc)),
            Request(5, "legacy-status", null,
                createdAt: new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Scheduling:TimeZoneId"] = "UTC" })
            .Build();
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new AdminAnalyticsService(db, new ClinicClock(config, time));

        var summary = await service.GetSummaryAsync();

        Assert.Equal(5, summary.TotalRequests);
        Assert.Equal(4, summary.ThisMonthRequests);
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
        Assert.Equal(1, summary.ByDay.Single(x => x.Date == "2026-08-31").Count);
        Assert.Equal(1, summary.ByDay.Single(x => x.Date == "2026-09-01").Count);
        Assert.Equal(1, summary.ByDay.Single(x => x.Date == "2026-09-02").Count);
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
    public async Task Summary_ConvertsUtcCreatedAtToClinicCalendarBeforeGrouping()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"analytics-timezone-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ApplicationDbContext(options);

        db.AppointmentRequests.Add(Request(
            10,
            AppointmentStatuses.Pending,
            new DateTime(2026, 10, 1, 10, 0, 0),
            createdAt: new DateTime(2026, 8, 31, 21, 30, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Scheduling:TimeZoneId"] = "Europe/Moscow" })
            .Build();
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero));
        var service = new AdminAnalyticsService(db, new ClinicClock(config, time));

        var summary = await service.GetSummaryAsync();

        Assert.Equal(1, summary.ThisMonthRequests);
        Assert.Equal(1, summary.ByDay.Single(x => x.Date == "2026-09-01").Count);
    }

    [Fact]
    public async Task Summary_OldHistoryStillAffectsLifetimeAggregatesButNotRecentSeries()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"analytics-history-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ApplicationDbContext(options);

        db.Doctors.AddRange(
            new Doctor { Id = 20, FullName = "Zulu Historical" },
            new Doctor { Id = 21, FullName = "Alpha Recent" });
        db.AppointmentRequests.AddRange(
            Request(20, AppointmentStatuses.Pending, new DateTime(2020, 1, 10, 10, 0, 0), doctorId: 20, patientId: 100,
                createdAt: new DateTime(2020, 1, 1, 8, 0, 0, DateTimeKind.Utc)),
            Request(21, AppointmentStatuses.Confirmed, new DateTime(2026, 9, 7, 10, 0, 0), doctorId: 21,
                comment: "[Заявка через чат] Recent",
                createdAt: new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Scheduling:TimeZoneId"] = "UTC" })
            .Build();
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new AdminAnalyticsService(db, new ClinicClock(config, time));

        var summary = await service.GetSummaryAsync();

        Assert.Equal(2, summary.TotalRequests);
        Assert.Equal(1, summary.ThisMonthRequests);
        Assert.Equal(1, summary.Statuses.Pending);
        Assert.Equal(1, summary.Statuses.Confirmed);
        Assert.Equal(1, summary.Sources.Registered);
        Assert.Equal(1, summary.Sources.Denta);
        Assert.Equal(1, summary.ByDay.Single(day => day.Date == "2026-09-06").Count);
        Assert.Equal(1, summary.ByDay.Sum(day => day.Count));

        // Equal lifetime counts retain the existing secondary doctor-name ordering.
        Assert.Collection(summary.ByDoctor,
            first =>
            {
                Assert.Equal(21, first.DoctorId);
                Assert.Equal("Alpha Recent", first.DoctorName);
                Assert.Equal(1, first.Count);
            },
            second =>
            {
                Assert.Equal(20, second.DoctorId);
                Assert.Equal("Zulu Historical", second.DoctorName);
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
        string? comment = null,
        DateTime? createdAt = null) => new()
    {
        Id = id,
        Phone = $"+70000000{id:000}",
        Status = status,
        AppointmentDate = appointmentDate,
        DoctorId = doctorId,
        PatientId = patientId,
        Comment = comment,
        CreatedAt = createdAt ?? new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc)
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
