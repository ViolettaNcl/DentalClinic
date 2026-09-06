using System.Reflection;
using DentalClinic.Data;
using DentalClinic.Hubs;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class NotificationServiceTests
{
    [Fact]
    public async Task NotifyAsync_WhenRealtimeDeliveryFails_PersistsNotificationAndReturnsNormally()
    {
        await using var db = CreateDb();
        db.Patients.Add(new Patient
        {
            Id = 42,
            FirstName = "Test",
            Email = "notification@example.test",
            PasswordHash = "hash"
        });
        await db.SaveChangesAsync();

        var service = new NotificationService(
            db,
            CreateThrowingHubContext(),
            NullLogger<NotificationService>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            service.NotifyAsync(42, "appointment_confirmed", "Confirmed", 123));

        Assert.Null(exception);
        var notification = Assert.Single(db.Notifications);
        Assert.Equal(42, notification.PatientId);
        Assert.Equal("appointment_confirmed", notification.Type);
        Assert.Equal("Confirmed", notification.Message);
        Assert.Equal(123, notification.RelatedId);
        Assert.Null(notification.IdempotencyKey);
    }

    [Fact]
    public async Task NotifyOnceAsync_ReplayedKey_CreatesOnlyOneDurableNotification()
    {
        await using var db = CreateDb();
        db.Patients.Add(new Patient
        {
            Id = 44,
            FirstName = "Test",
            Email = "notification-once@example.test",
            PasswordHash = "hash"
        });
        await db.SaveChangesAsync();

        var service = new NotificationService(
            db,
            CreateThrowingHubContext(),
            NullLogger<NotificationService>.Instance);

        var first = await service.NotifyOnceAsync(
            44,
            "appointment_reminder",
            "Reminder",
            321,
            "appointment-reminder:321");
        var second = await service.NotifyOnceAsync(
            44,
            "appointment_reminder",
            "Reminder",
            321,
            "appointment-reminder:321");

        Assert.True(first);
        Assert.False(second);
        var notification = Assert.Single(db.Notifications);
        Assert.Equal("appointment-reminder:321", notification.IdempotencyKey);
    }

    [Fact]
    public async Task NotifyOnceAsync_ReplayedKey_StillPersistsOtherTrackedMaintenanceState()
    {
        await using var db = CreateDb();
        var patient = new Patient
        {
            Id = 45,
            FirstName = "Test",
            Email = "notification-state@example.test",
            PasswordHash = "hash"
        };
        db.Patients.Add(patient);
        var appointment = new AppointmentRequest
        {
            Id = 500,
            PatientId = 45,
            FirstName = "Test",
            Phone = "+79990000000",
            Status = AppointmentStatuses.Confirmed,
            AppointmentDate = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1), DateTimeKind.Unspecified),
            ReminderSent = false,
            CreatedAt = DateTime.UtcNow
        };
        db.AppointmentRequests.Add(appointment);
        await db.SaveChangesAsync();

        var service = new NotificationService(
            db,
            CreateThrowingHubContext(),
            NullLogger<NotificationService>.Instance);

        Assert.True(await service.NotifyOnceAsync(
            45,
            "appointment_reminder",
            "Reminder",
            500,
            "appointment-reminder:500"));

        appointment.ReminderSent = true;
        Assert.False(await service.NotifyOnceAsync(
            45,
            "appointment_reminder",
            "Reminder",
            500,
            "appointment-reminder:500"));

        db.ChangeTracker.Clear();
        Assert.True((await db.AppointmentRequests.FindAsync(500))!.ReminderSent);
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.IdempotencyKey == "appointment-reminder:500"));
    }

    [Fact]
    public void NotificationModel_HasUniqueFilteredIdempotencyIndex()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(Notification));
        Assert.NotNull(entity);

        var index = Assert.Single(entity!.GetIndexes().Where(i =>
            i.Properties.Count == 1
            && i.Properties[0].Name == nameof(Notification.IdempotencyKey)));

        Assert.True(index.IsUnique);
        Assert.Equal("[IdempotencyKey] IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public async Task NotifyAdminsAsync_WhenRealtimeDeliveryFails_DoesNotFailCommittedCaller()
    {
        await using var db = CreateDb();
        var service = new NotificationService(
            db,
            CreateThrowingHubContext(),
            NullLogger<NotificationService>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            service.NotifyAdminsAsync("new_review", "New review", 77));

        Assert.Null(exception);
        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task NotifyAsync_StillCapsPersistedMessageToStorageLimit_WhenRealtimeFails()
    {
        await using var db = CreateDb();
        db.Patients.Add(new Patient
        {
            Id = 43,
            FirstName = "Test",
            Email = "notification-limit@example.test",
            PasswordHash = "hash"
        });
        await db.SaveChangesAsync();

        var service = new NotificationService(
            db,
            CreateThrowingHubContext(),
            NullLogger<NotificationService>.Instance);

        await service.NotifyAsync(43, "review_rejected", new string('x', 700), 9);

        var notification = Assert.Single(db.Notifications);
        Assert.Equal(550, notification.Message.Length);
        Assert.EndsWith("...", notification.Message, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"notification-service-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IHubContext<NotificationHub> CreateThrowingHubContext()
        => DispatchProxy.Create<IHubContext<NotificationHub>, ThrowingHubContextProxy>();

    private class ThrowingHubContextProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_Clients" => DispatchProxy.Create<IHubClients, ThrowingHubClientsProxy>(),
                "get_Groups" => DispatchProxy.Create<IGroupManager, NoopGroupManagerProxy>(),
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }

    private class ThrowingHubClientsProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.ReturnType == typeof(IClientProxy))
                return DispatchProxy.Create<IClientProxy, ThrowingClientProxy>();

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private class ThrowingClientProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IClientProxy.SendCoreAsync))
                return Task.FromException(new InvalidOperationException("SignalR unavailable"));

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private class NoopGroupManagerProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (typeof(Task).IsAssignableFrom(targetMethod?.ReturnType))
                return Task.CompletedTask;

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
