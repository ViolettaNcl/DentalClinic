namespace DentalClinic.Services;

/// <summary>
/// Closed domain for durable patient notifications. Keep this list aligned with
/// CK_Notifications_Type so invalid producer values fail before persistence and
/// are also rejected at the shared database boundary.
/// </summary>
public static class NotificationTypes
{
    public const string Welcome = "welcome";
    public const string AppointmentConfirmed = "appointment_confirmed";
    public const string AppointmentCancelled = "appointment_cancelled";
    public const string AppointmentCompleted = "appointment_completed";
    public const string AppointmentReminder = "appointment_reminder";
    public const string AppointmentFollowUp = "appointment_followup";
    public const string ReviewApproved = "review_approved";
    public const string ReviewRejected = "review_rejected";

    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
    {
        Welcome,
        AppointmentConfirmed,
        AppointmentCancelled,
        AppointmentCompleted,
        AppointmentReminder,
        AppointmentFollowUp,
        ReviewApproved,
        ReviewRejected
    };

    public static bool IsSupported(string? type)
        => type is not null && Supported.Contains(type);
}
