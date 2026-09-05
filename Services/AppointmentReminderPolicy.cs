using System.Linq.Expressions;
using DentalClinic.Models;

namespace DentalClinic.Services;

/// <summary>
/// Single source of truth for deciding which appointments receive a reminder
/// and for formatting the reminder text. The eligibility expression is shared
/// by EF Core production queries and unit tests, so the two cannot drift apart.
/// </summary>
public static class AppointmentReminderPolicy
{
    public static Expression<Func<AppointmentRequest, bool>> DueBetween(
        DateTime windowStart,
        DateTime windowEnd)
    {
        return request =>
            request.PatientId != null
            && request.Status == AppointmentStatuses.Confirmed
            && !request.ReminderSent
            && request.AppointmentDate != null
            && request.AppointmentDate >= windowStart
            && request.AppointmentDate < windowEnd;
    }

    public static string BuildMessage(DateTime appointmentDate)
    {
        return $"Напоминаем о приёме в Dental Clinic: {appointmentDate:dd.MM.yyyy} в {appointmentDate:HH:mm} 🦷";
    }
}
