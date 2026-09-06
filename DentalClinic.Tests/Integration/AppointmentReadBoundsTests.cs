using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class AppointmentReadBoundsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AppointmentReadBoundsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PatientFeed_KeepsActiveRows_BoundsHistory_AndSignalsTruncation()
    {
        var client = _factory.CreateClient();
        var email = $"appointment-bounds-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "Bounds",
            Email = email,
            Password = "password123"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var session = await client.GetFromJsonAsync<JsonElement>("/api/auth/session");
        var patientId = session.GetProperty("id").GetInt32();
        var marker = $"patient-bounds-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.AppointmentRequests.AddRange(
                Enumerable.Range(0, AppointmentReadPolicy.PatientHistoryLimit + 5)
                    .Select(i => new AppointmentRequest
                    {
                        PatientId = patientId,
                        FirstName = marker,
                        Phone = "+7 999 111 22 33",
                        Status = AppointmentStatuses.Completed,
                        CreatedAt = now.AddMinutes(-i - 10),
                        AppointmentDate = now.AddDays(-i - 1)
                    }));

            db.AppointmentRequests.AddRange(
                Enumerable.Range(0, 3)
                    .Select(i => new AppointmentRequest
                    {
                        PatientId = patientId,
                        FirstName = marker,
                        Phone = "+7 999 111 22 33",
                        Status = AppointmentStatuses.Pending,
                        CreatedAt = now.AddMinutes(-i),
                        AppointmentDate = now.AddDays(i + 1)
                    }));

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/AppointmentRequest/patient/{patientId}");
        var rows = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("X-Result-Truncated")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("X-History-Truncated")));
        Assert.Equal("false", Assert.Single(response.Headers.GetValues("X-Active-Truncated")));
        Assert.Equal(AppointmentReadPolicy.PatientHistoryLimit + 3, rows.GetArrayLength());
        Assert.Equal(3, rows.EnumerateArray().Count(row => row.GetProperty("status").GetString() == AppointmentStatuses.Pending));
    }

    [Fact]
    public async Task AdminFeed_BoundsHistory_WithoutDroppingRecentActiveRows()
    {
        var email = $"appointment-admin-bounds-{Guid.NewGuid():N}@example.com";
        const string password = "admin-test-password";
        var marker = $"admin-bounds-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Admins.Add(new Admin
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            });

            db.AppointmentRequests.AddRange(
                Enumerable.Range(0, AppointmentReadPolicy.AdminHistoryLimit + 5)
                    .Select(i => new AppointmentRequest
                    {
                        FirstName = marker,
                        Phone = "+7 999 444 55 66",
                        Status = AppointmentStatuses.Cancelled,
                        CreatedAt = now.AddMinutes(-i - 10),
                        AppointmentDate = now.AddDays(-i - 1)
                    }));

            db.AppointmentRequests.Add(new AppointmentRequest
            {
                FirstName = marker,
                Phone = "+7 999 444 55 66",
                Status = AppointmentStatuses.Pending,
                CreatedAt = now,
                AppointmentDate = now.AddDays(1)
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

        var response = await client.GetAsync("/api/AppointmentRequest/admin/all");
        var rows = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("X-Result-Truncated")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("X-History-Truncated")));
        Assert.True(rows.GetArrayLength() <= AppointmentReadPolicy.AdminActiveLimit + AppointmentReadPolicy.AdminHistoryLimit);
        Assert.Contains(rows.EnumerateArray(), row =>
            row.GetProperty("firstName").GetString() == marker
            && row.GetProperty("status").GetString() == AppointmentStatuses.Pending);
    }
}
