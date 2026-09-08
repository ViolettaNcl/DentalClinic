namespace DentalClinic.Services;

/// <summary>
/// Patient-facing copy for appointment actions that require clinic staff.
/// Never invent contact details: use the configured public phone when present,
/// otherwise direct the patient to the clinic's published Contacts page.
/// </summary>
public static class AppointmentPatientContactPolicy
{
    public static string ConfirmedCancellationBlocked(string? clinicPhone)
        => BuildConfirmedActionBlocked("отменить", clinicPhone);

    public static string ConfirmedRescheduleBlocked(string? clinicPhone)
        => BuildConfirmedActionBlocked("перенести", clinicPhone);

    private static string BuildConfirmedActionBlocked(string action, string? clinicPhone)
    {
        var phone = string.IsNullOrWhiteSpace(clinicPhone) ? null : clinicPhone.Trim();
        var prefix = $"Подтверждённую запись нельзя {action} самостоятельно";

        return phone is not null
            ? $"{prefix} — пожалуйста, позвоните администратору клиники: {phone}"
            : $"{prefix} — свяжитесь с администратором клиники через актуальные контакты на странице «Контакты».";
    }
}
