using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class AppointmentPatientContactPolicyTests
{
    private const string PlaceholderPhone = "+7 (499) 999-99-99";

    [Fact]
    public void ConfirmedCancellationBlocked_UsesConfiguredPhoneWhenPresent()
    {
        var message = AppointmentPatientContactPolicy.ConfirmedCancellationBlocked("  +357 22 000000  ");

        Assert.Contains("+357 22 000000", message);
        Assert.Contains("отменить", message);
        Assert.DoesNotContain(PlaceholderPhone, message);
    }

    [Fact]
    public void ConfirmedRescheduleBlocked_FallsBackToPublishedContactsWithoutInventingPhone()
    {
        var message = AppointmentPatientContactPolicy.ConfirmedRescheduleBlocked("   ");

        Assert.Contains("перенести", message);
        Assert.Contains("Контакты", message);
        Assert.DoesNotContain(PlaceholderPhone, message);
    }

    [Fact]
    public void AppointmentSurfaces_DoNotEmbedTheLegacyPlaceholderPhone()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var controller = File.ReadAllText(Path.Combine(root, "Controllers/AppointmentRequestController.cs"));
        var dashboard = File.ReadAllText(Path.Combine(root, "wwwroot/assets/js/managers/patient/patientDashboard.js"));

        Assert.DoesNotContain(PlaceholderPhone, controller);
        Assert.DoesNotContain(PlaceholderPhone, dashboard);
        Assert.Contains("PublicClinicProfile.FromConfiguration(configuration)", controller);
        Assert.Contains("AppointmentPatientContactPolicy.ConfirmedCancellationBlocked", controller);
        Assert.Contains("AppointmentPatientContactPolicy.ConfirmedRescheduleBlocked", controller);
    }
}
