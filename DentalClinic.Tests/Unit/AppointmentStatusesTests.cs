using DentalClinic.Models;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class AppointmentStatusesTests
{
    [Theory]
    [InlineData("PENDING", AppointmentStatuses.Pending)]
    [InlineData(" confirmed ", AppointmentStatuses.Confirmed)]
    [InlineData("cancelled", AppointmentStatuses.Cancelled)]
    [InlineData("completed", AppointmentStatuses.Completed)]
    public void TryNormalize_KnownStatus_ReturnsCanonicalValue(string input, string expected)
    {
        Assert.True(AppointmentStatuses.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(AppointmentStatuses.Pending, AppointmentStatuses.Confirmed, true)]
    [InlineData(AppointmentStatuses.Pending, AppointmentStatuses.Completed, false)]
    [InlineData(AppointmentStatuses.Confirmed, AppointmentStatuses.Completed, true)]
    [InlineData(AppointmentStatuses.Completed, AppointmentStatuses.Pending, false)]
    [InlineData(AppointmentStatuses.Cancelled, AppointmentStatuses.Pending, true)]
    public void CanTransition_EnforcesWorkflow(string current, string next, bool expected)
    {
        Assert.Equal(expected, AppointmentStatuses.CanTransition(current, next));
    }
}
