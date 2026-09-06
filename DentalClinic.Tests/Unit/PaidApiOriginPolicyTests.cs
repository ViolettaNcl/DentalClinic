using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class PaidApiOriginPolicyTests
{
    [Fact]
    public void SameOriginHttpsRequest_IsAllowed()
    {
        Assert.True(PaidApiOriginPolicy.IsAllowed(
            "https://clinic.example",
            null,
            "https",
            "clinic.example",
            null,
            allowDirectRequests: false));
    }

    [Fact]
    public void CrossOriginRequest_IsRejected()
    {
        Assert.False(PaidApiOriginPolicy.IsAllowed(
            "https://attacker.example",
            null,
            "https",
            "clinic.example",
            null,
            allowDirectRequests: false));
    }

    [Fact]
    public void SameOriginBrowserMetadataWithoutOrigin_IsAllowed()
    {
        Assert.True(PaidApiOriginPolicy.IsAllowed(
            null,
            "same-origin",
            "https",
            "clinic.example",
            null,
            allowDirectRequests: false));
    }

    [Fact]
    public void MissingBrowserMetadata_IsRejectedInProduction()
    {
        Assert.False(PaidApiOriginPolicy.IsAllowed(
            null,
            null,
            "https",
            "clinic.example",
            null,
            allowDirectRequests: false));
    }

    [Fact]
    public void MissingBrowserMetadata_CanBeAllowedForLocalAndTestTools()
    {
        Assert.True(PaidApiOriginPolicy.IsAllowed(
            null,
            null,
            "http",
            "localhost",
            5000,
            allowDirectRequests: true));
    }

    [Theory]
    [InlineData("https://clinic.example:444", "https", "clinic.example", null)]
    [InlineData("http://clinic.example", "https", "clinic.example", null)]
    [InlineData("not a uri", "https", "clinic.example", null)]
    public void PortSchemeOrMalformedOrigin_IsRejected(string origin, string scheme, string host, int? port)
    {
        Assert.False(PaidApiOriginPolicy.IsAllowed(
            origin,
            null,
            scheme,
            host,
            port,
            allowDirectRequests: false));
    }
}
