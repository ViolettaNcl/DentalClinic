using DentalClinic.Controllers;
using DentalClinic.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class PublicClinicProfileTests
{
    [Fact]
    public void FromConfiguration_TrimsContacts_AndAcceptsCompleteCoordinates()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Clinic:Phone"] = "  +357 22 000000  ",
            ["Clinic:Email"] = "  clinic@example.test ",
            ["Clinic:Address"] = "  Example address  ",
            ["Clinic:Hours"] = "  Mon-Sat 09:00-20:00  ",
            ["Clinic:Latitude"] = "34.7071",
            ["Clinic:Longitude"] = "33.0226"
        });

        var profile = PublicClinicProfile.FromConfiguration(config);

        Assert.Equal("+357 22 000000", profile.Phone);
        Assert.Equal("clinic@example.test", profile.Email);
        Assert.Equal("Example address", profile.Address);
        Assert.Equal("Mon-Sat 09:00-20:00", profile.Hours);
        Assert.Equal(34.7071, profile.Latitude);
        Assert.Equal(33.0226, profile.Longitude);
        Assert.True(profile.HasCoordinates);
    }

    [Theory]
    [InlineData("34.7", null)]
    [InlineData(null, "33.0")]
    [InlineData("91", "33.0")]
    [InlineData("34.7", "181")]
    [InlineData("NaN", "33.0")]
    [InlineData("34,7", "33.0")]
    public void FromConfiguration_RejectsIncompleteOrInvalidCoordinatePairs(
        string? latitude,
        string? longitude)
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Clinic:Latitude"] = latitude,
            ["Clinic:Longitude"] = longitude
        });

        var profile = PublicClinicProfile.FromConfiguration(config);

        Assert.Null(profile.Latitude);
        Assert.Null(profile.Longitude);
        Assert.False(profile.HasCoordinates);
    }

    [Fact]
    public void FromConfiguration_PublishesNoFabricatedDefaults_WhenClinicIsUnconfigured()
    {
        var profile = PublicClinicProfile.FromConfiguration(BuildConfiguration([]));

        Assert.Null(profile.Phone);
        Assert.Null(profile.Email);
        Assert.Null(profile.Address);
        Assert.Null(profile.Hours);
        Assert.Null(profile.Latitude);
        Assert.Null(profile.Longitude);
        Assert.False(profile.HasCoordinates);
    }

    [Fact]
    public void ClinicController_ReturnsConfiguredPublicProfile()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Clinic:Phone"] = "+357 22 000000",
            ["Clinic:Latitude"] = "34.7",
            ["Clinic:Longitude"] = "33.0"
        });
        var controller = new ClinicController(config);

        var result = controller.GetPublicProfile();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<PublicClinicProfile>(ok.Value);

        Assert.Equal("+357 22 000000", profile.Phone);
        Assert.True(profile.HasCoordinates);
    }

    private static IConfiguration BuildConfiguration(IEnumerable<KeyValuePair<string, string?>> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
