using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class DoctorDeactivationIntegrityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DoctorDeactivationIntegrityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(AppointmentStatuses.Pending)]
    [InlineData(AppointmentStatuses.Confirmed)]
    public async Task Deactivate_WithFutureActiveAppointment_ReturnsConflictAndKeepsDoctorActive(string status)
    {
        var doctorId = await SeedDoctorWithAppointmentAsync(
            status,
            DateTime.SpecifyKind(DateTime.UtcNow.AddDays(10), DateTimeKind.Unspecified));
        var client = await CreateAdminClientAsync();

        var response = await client.PutAsJsonAsync($"/api/doctor/{doctorId}", new UpdateDoctorRequest
        {
            IsActive = false
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("futureAppointments").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var doctor = await db.Doctors.AsNoTracking().SingleAsync(d => d.Id == doctorId);
        Assert.True(doctor.IsActive);
    }

    [Theory]
    [InlineData(AppointmentStatuses.Cancelled)]
    [InlineData(AppointmentStatuses.Completed)]
    public async Task Deactivate_WithOnlyNonActiveFutureAppointments_Succeeds(string status)
    {
        var doctorId = await SeedDoctorWithAppointmentAsync(
            status,
            DateTime.SpecifyKind(DateTime.UtcNow.AddDays(10), DateTimeKind.Unspecified));
        var client = await CreateAdminClientAsync();

        var response = await client.PutAsJsonAsync($"/api/doctor/{doctorId}", new UpdateDoctorRequest
        {
            IsActive = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var doctor = await db.Doctors.AsNoTracking().SingleAsync(d => d.Id == doctorId);
        Assert.False(doctor.IsActive);
    }

    [Fact]
    public async Task Deactivate_WithPastConfirmedAppointment_Succeeds()
    {
        var doctorId = await SeedDoctorWithAppointmentAsync(
            AppointmentStatuses.Confirmed,
            DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-10), DateTimeKind.Unspecified));
        var client = await CreateAdminClientAsync();

        var response = await client.PutAsJsonAsync($"/api/doctor/{doctorId}", new UpdateDoctorRequest
        {
            IsActive = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<int> SeedDoctorWithAppointmentAsync(string status, DateTime appointmentDate)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var doctor = new Doctor
        {
            FullName = $"Deactivation test doctor {Guid.NewGuid():N}",
            IsActive = true
        };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        db.AppointmentRequests.Add(new AppointmentRequest
        {
            FirstName = "Schedule patient",
            Phone = "+79990000000",
            DoctorId = doctor.Id,
            AppointmentDate = appointmentDate,
            Status = status,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return doctor.Id;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var email = $"doctor-deactivation-admin-{Guid.NewGuid():N}@example.com";
        const string password = "admin-test-password";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Admins.Add(new Admin
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/admin/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }
}
