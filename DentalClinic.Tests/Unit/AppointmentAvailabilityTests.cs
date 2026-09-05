using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class AppointmentAvailabilityTests
{
    [Fact]
    public async Task Availability_UsesThirtyMinuteStarts_AndBlocksPendingOverlap()
    {
        await using var db = CreateDb();
        var doctor = new Doctor { FullName = "Calendar test doctor", IsActive = true };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        db.AppointmentRequests.AddRange(
            Appointment(doctor.Id, new DateTime(2026, 9, 8, 10, 0, 0), AppointmentStatuses.Pending),
            Appointment(doctor.Id, new DateTime(2026, 9, 8, 14, 0, 0), AppointmentStatuses.Confirmed),
            Appointment(doctor.Id, new DateTime(2026, 9, 8, 16, 0, 0), AppointmentStatuses.Cancelled));
        await db.SaveChangesAsync();

        var service = CreateService(db, new DateTimeOffset(2026, 9, 7, 6, 0, 0, TimeSpan.Zero));
        var result = await service.GetAvailabilityAsync(
            doctor.Id,
            new DateOnly(2026, 9, 8),
            new DateOnly(2026, 9, 8));

        Assert.Equal(30, result.SlotIntervalMinutes);
        Assert.Equal(60, result.AppointmentDurationMinutes);

        var day = Assert.Single(result.Days);
        Assert.False(day.Closed);
        Assert.Equal("09:00", day.Open);
        Assert.Equal("20:00", day.Close);
        Assert.Contains(day.Slots, slot => slot.Time == "09:30");
        Assert.Contains(day.Slots, slot => slot.Time == "19:00");
        Assert.DoesNotContain(day.Slots, slot => slot.Time == "19:30");

        Assert.True(Slot(day, "09:00").IsAvailable);
        Assert.False(Slot(day, "09:30").IsAvailable);
        Assert.Equal(AppointmentStatuses.Pending, Slot(day, "09:30").BlockedReason);
        Assert.False(Slot(day, "10:00").IsAvailable);
        Assert.False(Slot(day, "10:30").IsAvailable);
        Assert.True(Slot(day, "11:00").IsAvailable);

        Assert.False(Slot(day, "13:30").IsAvailable);
        Assert.Equal(AppointmentStatuses.Confirmed, Slot(day, "14:00").BlockedReason);
        Assert.True(Slot(day, "16:00").IsAvailable); // cancelled appointments do not reserve time
    }

    [Fact]
    public async Task Availability_MarksClosedDays_AndLeadTimeAsUnavailable()
    {
        await using var db = CreateDb();
        var doctor = new Doctor { FullName = "Lead time doctor", IsActive = true };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        // 06:00 UTC == 09:00 Europe/Moscow on 2026-09-07.
        var service = CreateService(db, new DateTimeOffset(2026, 9, 7, 6, 0, 0, TimeSpan.Zero));
        var monday = await service.GetAvailabilityAsync(
            doctor.Id,
            new DateOnly(2026, 9, 7),
            new DateOnly(2026, 9, 7));

        var mondayDay = Assert.Single(monday.Days);
        Assert.False(Slot(mondayDay, "09:00").IsAvailable);
        Assert.Equal("lead-time", Slot(mondayDay, "09:00").BlockedReason);
        Assert.False(Slot(mondayDay, "09:30").IsAvailable);
        Assert.Equal("lead-time", Slot(mondayDay, "09:30").BlockedReason);
        Assert.True(Slot(mondayDay, "10:00").IsAvailable);

        var sunday = await service.GetAvailabilityAsync(
            doctor.Id,
            new DateOnly(2026, 9, 13),
            new DateOnly(2026, 9, 13));

        var sundayDay = Assert.Single(sunday.Days);
        Assert.True(sundayDay.Closed);
        Assert.Empty(sundayDay.Slots);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"availability-tests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AppointmentSchedulingService CreateService(
        ApplicationDbContext db,
        DateTimeOffset utcNow)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduling:TimeZoneId"] = "Europe/Moscow",
                ["Scheduling:MinimumLeadMinutes"] = "30",
                ["Scheduling:AppointmentDurationMinutes"] = "60",
                ["Scheduling:SlotIntervalMinutes"] = "30"
            })
            .Build();

        var clock = new ClinicClock(configuration, new FixedTimeProvider(utcNow));
        return new AppointmentSchedulingService(db, configuration, clock);
    }

    private static AppointmentRequest Appointment(int doctorId, DateTime date, string status) => new()
    {
        FirstName = "Test",
        Phone = "+79990000000",
        AppointmentDate = DateTime.SpecifyKind(date, DateTimeKind.Unspecified),
        DoctorId = doctorId,
        Status = status,
        CreatedAt = DateTime.UtcNow
    };

    private static SchedulingSlotAvailability Slot(SchedulingDayAvailability day, string time) =>
        Assert.Single(day.Slots.Where(slot => slot.Time == time));

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
