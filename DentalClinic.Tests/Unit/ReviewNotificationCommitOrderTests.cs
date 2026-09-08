using Xunit;

namespace DentalClinic.Tests.Unit;

public class ReviewNotificationCommitOrderTests
{
    [Fact]
    public void Moderation_PersistsNotificationBeforeCommit_AndDeliversRealtimeAfterCommit()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var controller = File.ReadAllText(Path.Combine(root, "Controllers/ReviewController.cs"));
        var service = File.ReadAllText(Path.Combine(root, "Services/NotificationService.cs"));

        var actionStart = controller.IndexOf("public async Task<IActionResult> Moderate(", StringComparison.Ordinal);
        var actionEnd = controller.IndexOf("private async Task<IDbContextTransaction?> BeginModerationTransactionAsync", actionStart, StringComparison.Ordinal);
        Assert.True(actionStart >= 0 && actionEnd > actionStart, "Review moderation action must be discoverable.");

        var action = controller[actionStart..actionEnd];
        var persistIndex = action.IndexOf("PersistPatientNotificationAsync", StringComparison.Ordinal);
        var commitIndex = action.IndexOf("await transaction.CommitAsync(cancellationToken)", persistIndex, StringComparison.Ordinal);
        var realtimeIndex = action.IndexOf("DeliverPersistedPatientRealtimeBestEffortAsync", StringComparison.Ordinal);

        Assert.True(persistIndex >= 0, "Durable patient notification must be persisted inside the moderation transaction.");
        Assert.True(commitIndex > persistIndex, "Moderation transaction must commit after durable notification persistence.");
        Assert.True(realtimeIndex > commitIndex, "Realtime delivery must happen only after the moderation transaction commits.");
        Assert.Contains("NotificationTypes.ReviewApproved", action, StringComparison.Ordinal);
        Assert.Contains("NotificationTypes.ReviewRejected", action, StringComparison.Ordinal);
        Assert.DoesNotContain("_notifications.NotifyAsync(", action, StringComparison.Ordinal);

        var persistMethodStart = service.IndexOf("public async Task<Notification> PersistPatientNotificationAsync", StringComparison.Ordinal);
        var deliverMethodStart = service.IndexOf("public Task DeliverPersistedPatientRealtimeBestEffortAsync", persistMethodStart, StringComparison.Ordinal);
        Assert.True(persistMethodStart >= 0 && deliverMethodStart > persistMethodStart, "Split durable/realtime notification API must exist.");

        var persistMethod = service[persistMethodStart..deliverMethodStart];
        Assert.Contains("await _db.SaveChangesAsync(cancellationToken);", persistMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("DeliverPatientRealtimeBestEffortAsync", persistMethod, StringComparison.Ordinal);
    }
}
