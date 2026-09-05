using DentalClinic.Models;
using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class AppointmentReminderPolicyTests
{
    private static readonly DateTime Start = new(2026, 9, 7, 9, 0, 0);
    private static readonly DateTime End = new(2026, 9, 7, 11, 0, 0);

    [Fact]
    public void DueBetween_IncludesConfirmedRegisteredUnsentAppointmentInsideWindow()
    {
        var predicate = AppointmentReminderPolicy.DueBetween(Start, End).Compile();
        var appointment = NewAppointment(Start.AddMinutes(30));

        Assert.True(predicate(appointment));
    }

    [Theory]
    [InlineData(null, "confirmed", false, true)]
    [InlineData(42, "pending", false, true)]
    [InlineData(42, "cancelled", false, true)]
    [InlineData(42, "completed", false, true)]
    [InlineData(42, "confirmed", true, true)]
    [InlineData(42, "confirmed", false, false)]
    public void DueBetween_ExcludesIneligibleAppointments(
        int? patientId,
        string status,
        bool reminderSent,
        bool hasDate)
    {
        var predicate = AppointmentReminderPolicy.DueBetween(Start, End).Compile();
        var appointment = NewAppointment(hasDate ? Start.AddMinutes(30) : null);
        appointment.PatientId = patientId;
        appointment.Status = status;
        appointment.ReminderSent = reminderSent;

        Assert.False(predicate(appointment));
    }

    [Fact]
    public void DueBetween_UsesInclusiveStartAndExclusiveEnd()
    {
        var predicate = AppointmentReminderPolicy.DueBetween(Start, End).Compile();

        Assert.True(predicate(NewAppointment(Start)));
        Assert.False(predicate(NewAppointment(End)));
        Assert.False(predicate(NewAppointment(Start.AddTicks(-1))));
    }

    [Fact]
    public void BuildMessage_UsesExactDateAndTime_AndDoesNotAssumeTomorrow()
    {
        var message = AppointmentReminderPolicy.BuildMessage(
            new DateTime(2026, 9, 8, 14, 35, 0));

        Assert.Equal("Напоминаем о приёме в Dental Clinic: 08.09.2026 в 14:35 🦷", message);
        Assert.DoesNotContain("завтра", message, StringComparison.OrdinalIgnoreCase);
    }

    private static AppointmentRequest NewAppointment(DateTime? appointmentDate) => new()
    {
        Id = 10,
        PatientId = 42,
        FirstName = "Test",
        Phone = "+7 900 000 00 00",
        AppointmentDate = appointmentDate,
        Status = AppointmentStatuses.Confirmed,
        ReminderSent = false,
        CreatedAt = DateTime.UtcNow
    };
}
