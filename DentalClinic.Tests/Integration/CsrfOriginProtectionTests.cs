using System.Net;
using System.Net.Http.Json;
using DentalClinic.Models;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class CsrfOriginProtectionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CsrfOriginProtectionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CrossOriginRegistration_IsRejected()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new RegisterRequest
            {
                FirstName = "Cross Site",
                Email = $"csrf-register-{Guid.NewGuid():N}@example.com",
                Password = "Password123!"
            })
        };
        request.Headers.TryAddWithoutValidation("Origin", "https://attacker.example");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CrossOriginCookieAuthenticatedProfileUpdate_IsRejected()
    {
        var client = _factory.CreateClient();
        var email = $"csrf-profile-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "Patient",
            Email = email,
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/profile")
        {
            Content = JsonContent.Create(new UpdateProfileRequest
            {
                FirstName = "Should Not Apply",
                Phone = "+1 555 0100"
            })
        };
        request.Headers.TryAddWithoutValidation("Origin", "https://attacker.example");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SameOriginCookieAuthenticatedProfileUpdate_IsAllowed()
    {
        var client = _factory.CreateClient();
        var email = $"csrf-same-origin-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "Patient",
            Email = email,
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/profile")
        {
            Content = JsonContent.Create(new UpdateProfileRequest
            {
                FirstName = "Updated",
                Phone = "+1 555 0101"
            })
        };
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
