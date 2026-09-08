using Xunit;

namespace DentalClinic.Tests.Unit;

public class RegistrationNotificationSafetyTests
{
    [Fact]
    public void Registration_UsesOptionalWelcomeNotificationAfterPatientCommit()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var source = File.ReadAllText(Path.Combine(root, "Controllers/AuthController.cs"));

        var saveIndex = source.IndexOf("await _db.SaveChangesAsync(cancellationToken);", StringComparison.Ordinal);
        var optionalNotificationIndex = source.IndexOf("TryNotifyOptionalAsync", StringComparison.Ordinal);

        Assert.True(saveIndex >= 0, "Registration must contain the durable patient save.");
        Assert.True(optionalNotificationIndex > saveIndex, "Welcome notification must happen only after patient persistence.");
        Assert.Contains("NotificationTypes.Welcome", source);
        Assert.DoesNotContain("NotifyAsync(\n                patient.Id,\n                \"welcome\"", source, StringComparison.Ordinal);
    }
}
