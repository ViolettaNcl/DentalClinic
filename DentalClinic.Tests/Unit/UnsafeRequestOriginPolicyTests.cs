using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class UnsafeRequestOriginPolicyTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void CookieAuthenticatedUnsafeApiRequests_RequireValidation(string method)
    {
        Assert.True(UnsafeRequestOriginPolicy.RequiresValidation(
            method,
            "/api/appointments/42",
            hasAuthCookie: true));
    }

    [Theory]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/admin/login")]
    [InlineData("/api/auth/logout")]
    public void SessionMutationEndpoints_RequireValidationWithoutExistingCookie(string path)
    {
        Assert.True(UnsafeRequestOriginPolicy.RequiresValidation(
            "POST",
            path,
            hasAuthCookie: false));
    }

    [Fact]
    public void BearerOnlyUnsafeApiRequest_DoesNotRequireCookieCsrfValidation()
    {
        Assert.False(UnsafeRequestOriginPolicy.RequiresValidation(
            "PUT",
            "/api/service/12",
            hasAuthCookie: false));
    }

    [Fact]
    public void SafeMethod_DoesNotRequireValidation()
    {
        Assert.False(UnsafeRequestOriginPolicy.RequiresValidation(
            "GET",
            "/api/auth/profile",
            hasAuthCookie: true));
    }

    [Fact]
    public void SignalRNegotiate_IsOutsideApiCsrfPolicy()
    {
        Assert.False(UnsafeRequestOriginPolicy.RequiresValidation(
            "POST",
            "/hubs/notifications/negotiate",
            hasAuthCookie: true));
    }

    [Fact]
    public void SameOriginOrigin_IsAllowed()
    {
        Assert.True(UnsafeRequestOriginPolicy.IsAllowed(
            "https://clinic.example",
            null,
            "same-origin",
            "https",
            "clinic.example",
            null,
            allowDirectRequests: false));
    }

    [Fact]
    public void SameOriginRefererFallback_IsAllowed()
    {
        Assert.True(UnsafeRequestOriginPolicy.IsAllowed(
            null,
            "https://clinic.example/patient-dashboard.html",
            "same-origin",
            "https",
            "clinic.example",
            null,
            allowDirectRequests: false));
    }

    [Fact]
    public void CrossOriginOrigin_IsRejected()
    {
        Assert.False(UnsafeRequestOriginPolicy.IsAllowed(
            "https://attacker.example",
            "https://clinic.example/page",
            "cross-site",
            "https",
            "clinic.example",
            null,
            allowDirectRequests: false));
    }

    [Fact]
    public void SpoofedSameOriginFetchMetadataWithoutOriginOrReferer_IsRejectedInProduction()
    {
        Assert.False(UnsafeRequestOriginPolicy.IsAllowed(
            null,
            null,
            "same-origin",
            "https",
            "clinic.example",
            null,
            allowDirectRequests: false));
    }

    [Fact]
    public void MissingBrowserMetadata_CanBeAllowedForDevelopmentAndTests()
    {
        Assert.True(UnsafeRequestOriginPolicy.IsAllowed(
            null,
            null,
            null,
            "http",
            "localhost",
            80,
            allowDirectRequests: true));
    }

    [Theory]
    [InlineData("https://clinic.example:444", "https", "clinic.example", null)]
    [InlineData("http://clinic.example", "https", "clinic.example", null)]
    [InlineData("not a uri", "https", "clinic.example", null)]
    public void PortSchemeOrMalformedOrigin_IsRejected(
        string origin,
        string scheme,
        string host,
        int? port)
    {
        Assert.False(UnsafeRequestOriginPolicy.IsAllowed(
            origin,
            null,
            null,
            scheme,
            host,
            port,
            allowDirectRequests: false));
    }
}
