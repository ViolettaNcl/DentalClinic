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

public class MaintenanceReminderIdempotencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MaintenanceReminderIdempotencyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TomorrowReminders_ReplayedMaintenanceRun_CreatesOneDurableNotification()
    {
        int appointmentId;
        int patientId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<ClinicClock>();
            var suffix = Guid.NewGuid().ToString("N");
            var patient = new Patient
            {
                FirstName = "Reminder",
                Email = $"reminder-{suffix}@example.test",
                Phone = "+7 900 000 00 02",
                PasswordHash = "test-hash"
            };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            patientId = patient.Id;

            var seededAppointment = new AppointmentRequest
            {
                PatientId = patient.Id,
                FirstName = patient.FirstName,
                Phone = patient.Phone!,
                AppointmentDate = clock.Now.Date.AddDays(1).AddHours(10),
                Status = AppointmentStatuses.Confirmed,
                ReminderSent = false,
                CreatedAt = DateTime.UtcNow
            };
            db.AppointmentRequests.Add(seededAppointment);
            await db.SaveChangesAsync();
            appointmentId = seededAppointment.Id;
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CustomWebApplicationFactory.CronSecret);

        var first = await client.GetAsync("/api/maintenance/reminders");
        var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var second = await client.GetAsync("/api/maintenance/reminders");
        var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(1, firstJson.RootElement.GetProperty("processed").GetInt32());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(0, secondJson.RootElement.GetProperty("processed").GetInt32());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var savedAppointment = await verifyDb.AppointmentRequests.AsNoTracking()
            .SingleAsync(a => a.Id == appointmentId);
        Assert.True(savedAppointment.ReminderSent);

        var notification = Assert.Single(await verifyDb.Notifications.AsNoTracking()
            .Where(n => n.PatientId == patientId
                        && n.Type == "appointment_reminder"
                        && n.RelatedId == appointmentId)
            .ToListAsync());
        Assert.Equal($"appointment-reminder:{appointmentId}", notification.IdempotencyKey);
    }
}
