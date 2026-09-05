using System.Net;
using System.Net.Http.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class AdminStatsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminStatsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Report_FiltersAndDisplaysCreatedAtByClinicTimeZone()
    {
        var email = $"report-admin-{Guid.NewGuid():N}@example.com";
        const string password = "report-password-123";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Admins.Add(new Admin
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            });

            // Europe/Moscow is UTC+3 in September. 22:30 UTC on Sep 1 belongs to
            // clinic date Sep 2 (01:30), while 20:30 UTC still belongs to Sep 1.
            db.AppointmentRequests.AddRange(
                new AppointmentRequest
                {
                    FirstName = "Inside clinic day",
                    Phone = "+79990000001",
                    Status = AppointmentStatuses.Pending,
                    CreatedAt = new DateTime(2026, 9, 1, 22, 30, 0, DateTimeKind.Utc)
                },
                new AppointmentRequest
                {
                    FirstName = "Outside clinic day",
                    Phone = "+79990000002",
                    Status = AppointmentStatuses.Pending,
                    CreatedAt = new DateTime(2026, 9, 1, 20, 30, 0, DateTimeKind.Utc)
                });

            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email,
            password
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var response = await client.GetAsync(
            "/api/adminstats/export/report?from=2026-09-02&to=2026-09-02");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Inside clinic day", html);
        Assert.Contains("02.09.2026 01:30", html);
        Assert.DoesNotContain("Outside clinic day", html);
        Assert.Contains("02.09.2026 – 02.09.2026", html);
    }
}
