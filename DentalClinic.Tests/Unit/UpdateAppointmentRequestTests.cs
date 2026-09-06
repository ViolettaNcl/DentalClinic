using System.Text.Json;
using DentalClinic.Models;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class UpdateAppointmentRequestTests
{
    [Fact]
    public void OmittedNullableFields_AreNotMarkedSpecified()
    {
        var request = JsonSerializer.Deserialize<UpdateAppointmentRequest>("{}")!;

        Assert.False(request.AppointmentDateSpecified);
        Assert.False(request.DoctorIdSpecified);
        Assert.False(request.CommentSpecified);
    }

    [Fact]
    public void ExplicitNullNullableFields_AreMarkedSpecified()
    {
        var request = JsonSerializer.Deserialize<UpdateAppointmentRequest>(
            "{\"appointmentDate\":null,\"doctorId\":null,\"comment\":null}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.True(request.AppointmentDateSpecified);
        Assert.True(request.DoctorIdSpecified);
        Assert.True(request.CommentSpecified);
        Assert.Null(request.AppointmentDate);
        Assert.Null(request.DoctorId);
        Assert.Null(request.Comment);
    }

    [Fact]
    public void SuppliedValues_AreMarkedSpecifiedAndRetained()
    {
        var request = JsonSerializer.Deserialize<UpdateAppointmentRequest>(
            "{\"appointmentDate\":\"2030-01-02T15:30:00\",\"doctorId\":42,\"comment\":\"updated\"}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.True(request.AppointmentDateSpecified);
        Assert.True(request.DoctorIdSpecified);
        Assert.True(request.CommentSpecified);
        Assert.Equal(new DateTime(2030, 1, 2, 15, 30, 0), request.AppointmentDate);
        Assert.Equal(42, request.DoctorId);
        Assert.Equal("updated", request.Comment);
    }
}
