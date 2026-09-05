using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class MaintenanceFollowUpTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MaintenanceFollowUpTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FollowUps_RejectsRequestWithoutCronSecret()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/maintenance/follow-ups");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FollowUps_SendsOnceForEligibleCompletedAppointment()
    {
        var client = _factory.CreateClient();
        int appointmentId;
        int patientId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<ClinicClock>();
            var suffix = Guid.NewGuid().ToString("N");

            var patient = new Patient
            {
                FirstName = "FollowUp",
                Email = $"followup-{suffix}@example.test",
                Phone = "+7 900 000 00 00",
                PasswordHash = "test-hash"
            };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            patientId = patient.Id;

            var eligible = new AppointmentRequest
            {
                PatientId = patient.Id,
                FirstName = patient.FirstName,
                Phone = patient.Phone!,
                AppointmentDate = clock.Now.AddDays(-1),
                Status = AppointmentStatuses.Completed,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };
            db.AppointmentRequests.Add(eligible);

            // These rows must never receive a post-visit follow-up.
            db.AppointmentRequests.Add(new AppointmentRequest
            {
                PatientId = null,
                FirstName = "Guest",
                Phone = "+7 900 000 00 01",
                AppointmentDate = clock.Now.AddDays(-1),
                Status = AppointmentStatuses.Completed,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            });
            db.AppointmentRequests.Add(new AppointmentRequest
            {
                PatientId = patient.Id,
                FirstName = patient.FirstName,
                Phone = patient.Phone!,
                AppointmentDate = clock.Now.AddDays(-1),
                Status = AppointmentStatuses.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            });

            await db.SaveChangesAsync();
            appointmentId = eligible.Id;
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CustomWebApplicationFactory.CronSecret);

        var first = await client.GetAsync("/api/maintenance/follow-ups");
        var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var second = await client.GetAsync("/api/maintenance/follow-ups");
        var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(1, firstJson.RootElement.GetProperty("processed").GetInt32());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(0, secondJson.RootElement.GetProperty("processed").GetInt32());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = await verifyDb.Notifications
            .Where(n => n.PatientId == patientId
                        && n.Type == AppointmentFollowUpPolicy.NotificationType
                        && n.RelatedId == appointmentId)
            .ToListAsync();

        var notification = Assert.Single(notifications);
        Assert.Contains("отзыв", notification.Message, StringComparison.OrdinalIgnoreCase);
    }
}
