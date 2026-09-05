using System.Net;
using System.Net.Http.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class AppointmentRequestControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AppointmentRequestControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_IgnoresServerManagedFields_FromRequestBody()
    {
        var client = _factory.CreateClient();
        var phone = UniquePhone();
        var date = NextOpenDayAt(10, 0);

        var response = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Тест",
            phone,
            appointmentDate = date,
            status = AppointmentStatuses.Confirmed,
            reminderSent = true,
            patientId = 999999,
            createdAt = new DateTime(2000, 1, 1)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = db.AppointmentRequests.Single(x => x.Phone == phone);

        Assert.Equal(AppointmentStatuses.Pending, saved.Status);
        Assert.False(saved.ReminderSent);
        Assert.Null(saved.PatientId);
        Assert.True(saved.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Create_WithoutPhone_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Тест",
            appointmentDate = NextOpenDayAt(10, 0)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidPhone_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Тест",
            phone = "not-a-phone",
            appointmentDate = NextOpenDayAt(10, 0)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDoctorButWithoutDate_ReturnsBadRequest()
    {
        int doctorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var doctor = new Doctor
            {
                FullName = $"Тестовый врач {Guid.NewGuid():N}",
                IsActive = true
            };
            db.Doctors.Add(doctor);
            await db.SaveChangesAsync();
            doctorId = doctor.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Тест",
            phone = UniquePhone(),
            doctorId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedBookingEndpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var client = _factory.CreateClient();

        var patientHistory = await client.GetAsync("/api/AppointmentRequest/patient/1");
        var adminList = await client.GetAsync("/api/AppointmentRequest/admin/all");
        var cancel = await client.PutAsync("/api/AppointmentRequest/1/cancel", null);
        var reschedule = await client.PutAsJsonAsync("/api/AppointmentRequest/1/reschedule", new
        {
            appointmentDate = NextOpenDayAt(12, 0)
        });

        Assert.Equal(HttpStatusCode.Unauthorized, patientHistory.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, adminList.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reschedule.StatusCode);
    }

    [Fact]
    public async Task Create_WithPastDate_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Тест",
            phone = UniquePhone(),
            appointmentDate = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-2).Date.AddHours(10), DateTimeKind.Unspecified)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDateOnlyPreference_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Тест",
            phone = UniquePhone(),
            appointmentDate = NextOpenDayAt(0, 0)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_OutsideWorkingHours_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Тест",
            phone = UniquePhone(),
            appointmentDate = NextOpenDayAt(20, 0)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithOverlappingDoctorSlot_ReturnsConflict()
    {
        int doctorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var doctor = new Doctor
            {
                FullName = $"Тестовый врач {Guid.NewGuid():N}",
                IsActive = true
            };
            db.Doctors.Add(doctor);
            await db.SaveChangesAsync();
            doctorId = doctor.Id;
        }

        var client = _factory.CreateClient();
        var date = NextOpenDayAt(11, 0);

        var first = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Первый пациент",
            phone = UniquePhone(),
            appointmentDate = date,
            doctorId
        });
        var second = await client.PostAsJsonAsync("/api/AppointmentRequest", new
        {
            firstName = "Второй пациент",
            phone = UniquePhone(),
            appointmentDate = date.AddMinutes(30),
            doctorId
        });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task MaintenanceEndpoint_WithoutCronSecret_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/maintenance/reminders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string UniquePhone() => $"+7999{Random.Shared.Next(1000000, 9999999)}";

    private static DateTime NextOpenDayAt(int hour, int minute)
    {
        var date = DateTime.UtcNow.Date.AddDays(3);
        while (date.DayOfWeek == DayOfWeek.Sunday)
            date = date.AddDays(1);

        return DateTime.SpecifyKind(date.AddHours(hour).AddMinutes(minute), DateTimeKind.Unspecified);
    }
}
