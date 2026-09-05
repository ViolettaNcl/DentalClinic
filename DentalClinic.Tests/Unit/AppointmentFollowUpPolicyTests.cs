using DentalClinic.Models;
using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class AppointmentFollowUpPolicyTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 12, 0, 0);
    private static readonly DateTime End = new(2026, 9, 6, 12, 0, 0);

    [Fact]
    public void DueBetween_IncludesCompletedRegisteredAppointmentInsideWindow()
    {
        var predicate = AppointmentFollowUpPolicy.DueBetween(Start, End).Compile();
        var appointment = NewAppointment(Start.AddDays(2));

        Assert.True(predicate(appointment));
    }

    [Theory]
    [InlineData(null, "completed", true)]
    [InlineData(42, "pending", true)]
    [InlineData(42, "confirmed", true)]
    [InlineData(42, "cancelled", true)]
    [InlineData(42, "completed", false)]
    public void DueBetween_ExcludesIneligibleAppointments(int? patientId, string status, bool hasDate)
    {
        var predicate = AppointmentFollowUpPolicy.DueBetween(Start, End).Compile();
        var appointment = NewAppointment(hasDate ? Start.AddDays(2) : null);
        appointment.PatientId = patientId;
        appointment.Status = status;

        Assert.False(predicate(appointment));
    }

    [Fact]
    public void DueBetween_UsesInclusiveStartAndExclusiveEnd()
    {
        var predicate = AppointmentFollowUpPolicy.DueBetween(Start, End).Compile();

        Assert.True(predicate(NewAppointment(Start)));
        Assert.False(predicate(NewAppointment(End)));
        Assert.False(predicate(NewAppointment(Start.AddTicks(-1))));
    }

    [Fact]
    public void BuildMessage_ReferencesVisitDateAndReviewAction()
    {
        var message = AppointmentFollowUpPolicy.BuildMessage(new DateTime(2026, 9, 4, 15, 0, 0));

        Assert.Contains("04.09.2026", message, StringComparison.Ordinal);
        Assert.Contains("отзыв", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("личном кабинете", message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("appointment_followup", AppointmentFollowUpPolicy.NotificationType);
    }

    private static AppointmentRequest NewAppointment(DateTime? appointmentDate) => new()
    {
        Id = 55,
        PatientId = 42,
        FirstName = "Test",
        Phone = "+7 900 000 00 00",
        AppointmentDate = appointmentDate,
        Status = AppointmentStatuses.Completed,
        CreatedAt = DateTime.UtcNow
    };
}
