using System.Text.Json;
using DentalClinic.Controllers;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DoctorScheduleControllerTests
{
    [Theory]
    [InlineData("04/01/2026", "2026-04-07")]
    [InlineData("2026-04-01T00:00:00", "2026-04-07")]
    [InlineData("2026-4-1", "2026-04-07")]
    public async Task GetSchedule_RequiresInvariantIsoDates(string from, string to)
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.GetSchedule(1, from, to, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetSchedule_RejectsReversedAndOversizedRanges()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>(
            await controller.GetSchedule(1, "2026-04-07", "2026-04-01", CancellationToken.None));
        Assert.IsType<BadRequestObjectResult>(
            await controller.GetSchedule(1, "2026-04-01", "2026-05-03", CancellationToken.None));
    }

    [Fact]
    public async Task GetSchedule_RejectsMissingOrInactiveDoctorBeforeQueryingAppointments()
    {
        await using var db = CreateDb();
        db.Doctors.Add(new Doctor { Id = 2, FullName = "Inactive", IsActive = false });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>(
            await controller.GetSchedule(999, "2026-04-01", "2026-04-07", CancellationToken.None));
        Assert.IsType<BadRequestObjectResult>(
            await controller.GetSchedule(2, "2026-04-01", "2026-04-07", CancellationToken.None));
    }

    [Fact]
    public async Task GetSchedule_ReturnsOnlyConfirmedAppointmentsInsideInclusiveDateRange()
    {
        await using var db = CreateDb();
        db.Doctors.Add(new Doctor { Id = 3, FullName = "Active", IsActive = true });
        db.AppointmentRequests.AddRange(
            Appointment(3, "Inside morning", new DateTime(2026, 4, 1, 9, 0, 0), AppointmentStatuses.Confirmed),
            Appointment(3, "Inside end", new DateTime(2026, 4, 7, 23, 59, 59), AppointmentStatuses.Confirmed),
            Appointment(3, "Pending", new DateTime(2026, 4, 2, 10, 0, 0), AppointmentStatuses.Pending),
            Appointment(3, "Outside", new DateTime(2026, 4, 8, 0, 0, 0), AppointmentStatuses.Confirmed));
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var action = await controller.GetSchedule(3, "2026-04-01", "2026-04-07", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("Inside morning", json, StringComparison.Ordinal);
        Assert.Contains("Inside end", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Pending", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Outside", json, StringComparison.Ordinal);
        Assert.True(json.IndexOf("Inside morning", StringComparison.Ordinal) < json.IndexOf("Inside end", StringComparison.Ordinal));
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"doctor-schedule-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static DoctorScheduleController CreateController(ApplicationDbContext db)
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

        var clock = new ClinicClock(configuration, TimeProvider.System);
        var scheduling = new AppointmentSchedulingService(db, configuration, clock);
        return new DoctorScheduleController(db, scheduling);
    }

    private static AppointmentRequest Appointment(int doctorId, string name, DateTime date, string status) => new()
    {
        FirstName = name,
        Phone = "+79990000000",
        AppointmentDate = DateTime.SpecifyKind(date, DateTimeKind.Unspecified),
        DoctorId = doctorId,
        Status = status,
        CreatedAt = DateTime.UtcNow
    };
}
