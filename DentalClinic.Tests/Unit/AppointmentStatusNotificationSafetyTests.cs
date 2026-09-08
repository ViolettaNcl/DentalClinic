using Xunit;

namespace DentalClinic.Tests.Unit;

public class AppointmentStatusNotificationSafetyTests
{
    [Fact]
    public void AdminStatusUpdate_UsesOptionalNotificationOnlyAfterAppointmentCommit()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var source = File.ReadAllText(Path.Combine(root, "Controllers/AppointmentRequestController.cs"));

        var updateStart = source.IndexOf("public async Task<IActionResult> Update(", StringComparison.Ordinal);
        var updateEnd = source.IndexOf("// Создать запись по телефону", updateStart, StringComparison.Ordinal);
        Assert.True(updateStart >= 0 && updateEnd > updateStart, "Admin appointment update action must be discoverable.");

        var updateSource = source[updateStart..updateEnd];
        var saveIndex = updateSource.IndexOf("await _context.SaveChangesAsync(cancellationToken);", StringComparison.Ordinal);
        var commitIndex = updateSource.IndexOf("await transaction.CommitAsync(cancellationToken)", StringComparison.Ordinal);
        var optionalNotificationIndex = updateSource.IndexOf("TryNotifyOptionalAsync", StringComparison.Ordinal);

        Assert.True(saveIndex >= 0, "Appointment update must persist the primary mutation.");
        Assert.True(commitIndex > saveIndex, "Relational scheduling transaction must commit after persistence.");
        Assert.True(optionalNotificationIndex > commitIndex, "Patient notification must remain a post-commit optional side effect.");
        Assert.Contains("NotificationTypes.AppointmentConfirmed", updateSource);
        Assert.Contains("NotificationTypes.AppointmentCancelled", updateSource);
        Assert.Contains("NotificationTypes.AppointmentCompleted", updateSource);
        Assert.Contains("request.Id,\n                    cancellationToken", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_notifications.NotifyAsync(", updateSource, StringComparison.Ordinal);
    }
}
