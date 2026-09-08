using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class NotificationTypeDomainTests
{
    private static readonly string[] Expected =
    [
        "welcome",
        "appointment_confirmed",
        "appointment_cancelled",
        "appointment_completed",
        "appointment_reminder",
        "appointment_followup",
        "review_approved",
        "review_rejected"
    ];

    [Fact]
    public void RuntimeDomain_ContainsEveryDurableNotificationTypeAndRejectsUnknownValues()
    {
        foreach (var type in Expected)
            Assert.True(NotificationTypes.IsSupported(type), type);

        Assert.False(NotificationTypes.IsSupported(null));
        Assert.False(NotificationTypes.IsSupported(string.Empty));
        Assert.False(NotificationTypes.IsSupported("new_review"));
        Assert.False(NotificationTypes.IsSupported(" appointment_confirmed "));
    }

    [Fact]
    public void Migration_FailsClosedAndDoesNotRewriteLegacyNotificationTypes()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var path = Path.Combine(root, "Migrations/20260909005000_ConstrainNotificationTypes.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("THROW 51032", source);
        Assert.Contains("CK_Notifications_Type", source);
        foreach (var type in Expected)
            Assert.Contains($"'{type}'", source);

        Assert.DoesNotContain("UPDATE [dbo].[Notifications]", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM [dbo].[Notifications]", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EfModel_DeclaresTheSameNotificationTypeConstraint()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var source = File.ReadAllText(Path.Combine(root, "Data/ApplicationDbContext.cs"));

        Assert.Contains("CK_Notifications_Type", source);
        foreach (var type in Expected)
            Assert.Contains($"'{type}'", source);
    }
}
