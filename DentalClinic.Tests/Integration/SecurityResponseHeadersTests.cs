using System.Net;
using System.Net.Http.Json;
using DentalClinic.Models;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class SecurityResponseHeadersTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SecurityResponseHeadersTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PublicResponse_HasConservativeBrowserSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("SAMEORIGIN", Header(response, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
        Assert.Equal("none", Header(response, "X-Permitted-Cross-Domain-Policies"));
    }

    [Fact]
    public async Task AuthenticatedApiResponse_IsNotCacheable()
    {
        var client = _factory.CreateClient();
        var email = $"security-headers-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "Security",
            Email = email,
            Password = "password123"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var profile = await client.GetAsync("/api/auth/profile");

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Contains("no-store", Header(profile, "Cache-Control"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-cache", Header(profile, "Pragma"));
    }

    private static string Header(HttpResponseMessage response, string name)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values), $"Missing response header: {name}");
        return string.Join(",", values);
    }
}
