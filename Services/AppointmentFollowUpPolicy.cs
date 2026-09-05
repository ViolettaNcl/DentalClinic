using System.Linq.Expressions;
using DentalClinic.Models;

namespace DentalClinic.Services;

/// <summary>
/// Eligibility and copy for automatic post-visit follow-up. No extra column is
/// required on AppointmentRequests: an existing notification with this type and
/// appointment RelatedId is the idempotency marker.
/// </summary>
public static class AppointmentFollowUpPolicy
{
    public const string NotificationType = "appointment_followup";

    public static Expression<Func<AppointmentRequest, bool>> DueBetween(
        DateTime windowStart,
        DateTime windowEnd)
    {
        return request =>
            request.PatientId != null
            && request.Status == AppointmentStatuses.Completed
            && request.AppointmentDate != null
            && request.AppointmentDate >= windowStart
            && request.AppointmentDate < windowEnd;
    }

    public static string BuildMessage(DateTime appointmentDate)
    {
        return $"Спасибо, что были в Dental Clinic {appointmentDate:dd.MM.yyyy}. Если вам удобно, оставьте отзыв в личном кабинете — это помогает нам улучшать сервис 💬";
    }
}
