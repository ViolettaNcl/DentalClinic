using System.Net;
using System.Net.Http.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class AppointmentSchedulingGuardTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AppointmentSchedulingGuardTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_WithInactiveDoctor_ReturnsBadRequest()
    {
        int doctorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var doctor = new Doctor
            {
                FullName = $"Inactive test doctor {Guid.NewGuid():N}",
                IsActive = false
            };
            db.Doctors.Add(doctor);
            await db.SaveChangesAsync();
            doctorId = doctor.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Test",
            phone = UniquePhone(),
            appointmentDate = NextOpenDayAt(10, 0),
            doctorId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithMisalignedSlot_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Test",
            phone = UniquePhone(),
            appointmentDate = NextOpenDayAt(10, 15)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DateOnlyOnClosedSunday_ReturnsBadRequest()
    {
        var date = DateTime.UtcNow.Date.AddDays(2);
        while (date.DayOfWeek != DayOfWeek.Sunday)
            date = date.AddDays(1);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Test",
            phone = UniquePhone(),
            appointmentDate = DateTime.SpecifyKind(date, DateTimeKind.Unspecified)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DoctorAvailability_WithoutAdminAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            "/api/doctorschedule/availability?doctorId=1&from=2026-09-07&to=2026-09-13");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string UniquePhone() => $"+7998{Random.Shared.Next(1000000, 9999999)}";

    private static DateTime NextOpenDayAt(int hour, int minute)
    {
        var date = DateTime.UtcNow.Date.AddDays(3);
        while (date.DayOfWeek == DayOfWeek.Sunday)
            date = date.AddDays(1);

        return DateTime.SpecifyKind(date.AddHours(hour).AddMinutes(minute), DateTimeKind.Unspecified);
    }
}
